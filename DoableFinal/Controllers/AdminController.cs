using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Services;

namespace DoableFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly TimelineAdjustmentService _timelineAdjustmentService;
        private readonly NotificationService _notificationService;
        private readonly HomePageService _homePageService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, TimelineAdjustmentService timelineAdjustmentService, NotificationService notificationService, HomePageService homePageService, ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _timelineAdjustmentService = timelineAdjustmentService;
            _notificationService = notificationService;
            _homePageService = homePageService;
            _logger = logger;
        }

        // Map display role names to the actual identity role (seeded roles)
        private string NormalizeToIdentityRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return role;
            if (role == "Project Manager" || role == "ProjectManager") return "Project Manager";
            return role;
        }

        private async Task<List<ApplicationUser>> GetUsersInRoleVariantsAsync(string role)
        {
            var normalized = NormalizeToIdentityRole(role);
            var users = new List<ApplicationUser>();
            var inIdentityRole = await _userManager.GetUsersInRoleAsync(normalized);
            users.AddRange(inIdentityRole);
            // if display role differs (with space), attempt retrieving users by that identity role too
            if (normalized != role)
            {
                try
                {
                    var other = await _userManager.GetUsersInRoleAsync(role);
                    foreach (var u in other)
                    {
                        if (!users.Any(x => x.Id == u.Id)) users.Add(u);
                    }
                }
                catch
                {
                    // ignore if such identity role doesn't exist
                }
            }
            return users.Distinct().ToList();
        }

        // Define the list of keys that are expected to accept images
        private static readonly HashSet<string> _imageSectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "hero-image",
            "about-image",
            "services-hero-image",
            "contact-hero-image",
            "feature-image",
            "team-member-image",
            "testimonial-image"
        };

        private static readonly HashSet<string> _contentImageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Index",
            "Contact",
            "Services"
        };

        public async Task<IActionResult> Notifications()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            var notifications = await _context.Notifications
                .Where(n => n.UserId == currentUser.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }

        // List inquiries submitted via the contact form
        public async Task<IActionResult> Inquiries()
        {
            var inquiries = await _context.Inquiries
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(inquiries);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkInquiryHandled(int id)
        {
            var inquiry = await _context.Inquiries.FindAsync(id);
            if (inquiry == null) return NotFound();

            inquiry.IsHandled = true;
            await _context.SaveChangesAsync();

            TempData["InquiryMessage"] = "Inquiry marked as handled.";
            return RedirectToAction(nameof(Inquiries));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationAsRead(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == currentUser.Id);

            if (notification == null)
            {
                return NotFound();
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Notifications));
        }

        public async Task<IActionResult> Index()
        {
            // Get counts for dashboard statistics
            ViewBag.TotalProjects = await _context.Projects.CountAsync(p => !p.IsArchived);
            ViewBag.TotalTasks = await _context.Tasks.CountAsync(t => !t.IsArchived);
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.CompletedTasks = await _context.Tasks.CountAsync(t => !t.IsArchived && t.Status == "Completed");
            ViewBag.OverdueTasks = await _context.Tasks.CountAsync(t => !t.IsArchived && t.Status != "Completed" && t.DueDate < DateTime.UtcNow);

            // Get recent users
            ViewBag.RecentUsers = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Get recent projects
            ViewBag.RecentProjects = await _context.Projects
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .Where(p => !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Get recent tasks - Update to handle multiple assignments
            ViewBag.RecentTasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Where(t => !t.IsArchived)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View();
        }

        // Consolidated User Management
        public async Task<IActionResult> Users(string roleFilter = "")
        {
            var users = await _context.Users
                .Where(u => !u.IsArchived)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            if (!string.IsNullOrEmpty(roleFilter))
            {
                users = users.Where(u => u.Role == roleFilter).ToList();
            }

            ViewBag.RoleFilter = roleFilter;
            return View(users);
        }

        public async Task<IActionResult> ArchivedUsers(string roleFilter = "")
        {
            var users = await _context.Users
                .Where(u => u.IsArchived)
                .OrderByDescending(u => u.ArchivedAt)
                .ToListAsync();

            if (!string.IsNullOrEmpty(roleFilter))
            {
                users = users.Where(u => u.Role == roleFilter).ToList();
            }

            ViewBag.RoleFilter = roleFilter;
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                // Don't allow archiving the last admin
                if (user.Role == "Admin")
                {
                    var adminCount = await _context.Users.CountAsync(u => u.Role == "Admin" && !u.IsArchived);
                    if (adminCount <= 1)
                    {
                        TempData["ErrorMessage"] = "Cannot archive the last admin user.";
                        return RedirectToAction(nameof(Users));
                    }
                }

                user.IsArchived = true;
                user.IsActive = false; // Set user as inactive when archived
                user.ArchivedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                // Get list of admins to notify
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                foreach (var admin in admins)
                {
                    var notification = new Notification
                    {
                        UserId = admin.Id,
                        Title = "User Management Update",
                        Message = $"User {user.FirstName} {user.LastName} has been archived.",
                        Link = "/Admin/ArchivedUsers",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false,
                        Type = NotificationType.General
                    };
                    _context.Notifications.Add(notification);
                }
                await _context.SaveChangesAsync();
                
                TempData["UserManagementMessage"] = $"User {user.FirstName} {user.LastName} has been archived.";
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnarchiveUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.IsArchived = false;
                user.IsActive = true; // Reactivate user when unarchived
                user.ArchivedAt = null;
                await _userManager.UpdateAsync(user);
                
                TempData["UserManagementMessage"] = $"User {user.FirstName} {user.LastName} has been unarchived.";
            }
            return RedirectToAction(nameof(ArchivedUsers));
        }

        [HttpGet]
        public IActionResult CreateUser(string role = "Employee")
        {
            ViewBag.Role = role;
            var viewModel = new CreateUserViewModel
            {
                Role = role
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            // Log the received values for debugging
            _logger.LogInformation($"Received form data - Email: {model.Email}, Role: {model.Role}, Password present: {!string.IsNullOrEmpty(model.Password)}");
            _logger.LogInformation($"MobileNumber RAW: '{model.MobileNumber}' (null:{model.MobileNumber == null}, empty:{string.IsNullOrEmpty(model.MobileNumber)}, whitespace:{string.IsNullOrWhiteSpace(model.MobileNumber)})");
            _logger.LogInformation($"TinNumber RAW: '{model.TinNumber}' (null:{model.TinNumber == null}, empty:{string.IsNullOrEmpty(model.TinNumber)}, whitespace:{string.IsNullOrWhiteSpace(model.TinNumber)})");
            _logger.LogInformation($"PagIbigAccount RAW: '{model.PagIbigAccount}' (null:{model.PagIbigAccount == null}, empty:{string.IsNullOrEmpty(model.PagIbigAccount)}, whitespace:{string.IsNullOrWhiteSpace(model.PagIbigAccount)})");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning($"ModelState is invalid. Total errors: {ModelState.ErrorCount}");
                foreach (var modelStateKey in ModelState.Keys)
                {
                    var modelStateVal = ModelState[modelStateKey];
                    if (modelStateVal.Errors.Count > 0)
                    {
                        _logger.LogError($"Field: {modelStateKey}");
                        foreach (var error in modelStateVal.Errors)
                        {
                            _logger.LogError($"  Error: {error.ErrorMessage}");
                        }
                    }
                }
                ViewBag.Role = model.Role;
                return View(model);
            }

            // Additional validation for password
            if (string.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError("Password", "Password is required");
                ViewBag.Role = model.Role;
                return View(model);
            }

            // Extra server-side validation: if Birthday is provided, ensure age >= 18
            if (model.Birthday.HasValue && model.Birthday.Value > DateTime.Today.AddYears(-18))
            {
                ModelState.AddModelError("Birthday", "User must be at least 18 years old.");
                ViewBag.Role = model.Role;
                return View(model);
            }

            try
            {
                // STRICT backend validation for all required fields based on role
                bool isEmployeeRole = model.Role == "Employee" || model.Role == "Project Manager" || model.Role == "Admin";
                bool isClientRole = model.Role == "Client";

                var phoneAttr = new DoableFinal.Validation.PhonePHAttribute();
                var tinAttr = new DoableFinal.Validation.TinAttribute();
                var pagIbigAttr = new DoableFinal.Validation.PagIbigAttribute();

                // Validate required fields for Employee/Project Manager/Admin
                if (isEmployeeRole)
                {
                    // Validate that required fields are provided
                    if (string.IsNullOrWhiteSpace(model.ResidentialAddress))
                    {
                        ModelState.AddModelError("ResidentialAddress", "Residential Address is required for Employees and Project Managers.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    // Trim and validate Mobile Number
                    var trimmedMobile = model.MobileNumber?.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedMobile))
                    {
                        ModelState.AddModelError("MobileNumber", "Mobile Number is required.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    if (!model.Birthday.HasValue)
                    {
                        ModelState.AddModelError("Birthday", "Birthday is required for Employees and Project Managers.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    // Trim and validate TIN Number
                    var trimmedTin = model.TinNumber?.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedTin))
                    {
                        ModelState.AddModelError("TinNumber", "TIN Number is required.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    // Trim and validate Pag-IBIG
                    var trimmedPagIbig = model.PagIbigAccount?.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedPagIbig))
                    {
                        ModelState.AddModelError("PagIbigAccount", "Pag-IBIG Account is required.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    if (string.IsNullOrWhiteSpace(model.Position))
                    {
                        ModelState.AddModelError("Position", "Position is required for Employees and Project Managers.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    // Validate format of required fields
                    if (!phoneAttr.IsValid(trimmedMobile))
                    {
                        ModelState.AddModelError("MobileNumber", "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits).");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    if (!tinAttr.IsValid(trimmedTin))
                    {
                        ModelState.AddModelError("TinNumber", "Invalid TIN. Expected: XXX-XXX-XXX or XXX-XXX-XXX-XXX.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    if (!pagIbigAttr.IsValid(trimmedPagIbig))
                    {
                        ModelState.AddModelError("PagIbigAccount", "Invalid Pag-IBIG MID. Expected 12 numeric digits.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }
                }

                // Validate required fields for Client
                if (isClientRole)
                {
                    if (string.IsNullOrWhiteSpace(model.CompanyName))
                    {
                        ModelState.AddModelError("CompanyName", "Company Name is required for Clients.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    if (string.IsNullOrWhiteSpace(model.CompanyAddress))
                    {
                        ModelState.AddModelError("CompanyAddress", "Company Address is required for Clients.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    if (string.IsNullOrWhiteSpace(model.CompanyType))
                    {
                        ModelState.AddModelError("CompanyType", "Company Type is required for Clients.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    if (string.IsNullOrWhiteSpace(model.Designation))
                    {
                        ModelState.AddModelError("Designation", "Designation is required for Clients.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    // Trim and validate Mobile Number
                    var trimmedMobile = model.MobileNumber?.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedMobile))
                    {
                        ModelState.AddModelError("MobileNumber", "Mobile Number is required.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    // Trim and validate TIN Number
                    var trimmedTin = model.TinNumber?.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedTin))
                    {
                        ModelState.AddModelError("TinNumber", "TIN Number is required.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    // Validate format of required fields
                    if (!phoneAttr.IsValid(trimmedMobile))
                    {
                        ModelState.AddModelError("MobileNumber", "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits).");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }

                    if (!tinAttr.IsValid(trimmedTin))
                    {
                        ModelState.AddModelError("TinNumber", "Invalid TIN. Expected: XXX-XXX-XXX or XXX-XXX-XXX-XXX.");
                        ViewBag.Role = model.Role;
                        return View(model);
                    }
                }

                // Check for existing email BEFORE creating user
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                    ViewBag.Role = model.Role;
                    return View(model);
                }

                // All validations passed - create the user
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Role = model.Role,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = true
                };

                // Set role-specific fields
                if (isEmployeeRole)
                {
                    user.ResidentialAddress = model.ResidentialAddress;
                    user.MobileNumber = model.MobileNumber;
                    user.Birthday = model.Birthday;
                    user.TinNumber = model.TinNumber;
                    user.PagIbigAccount = model.PagIbigAccount;
                    user.Position = model.Position;
                    user.EmailNotificationsEnabled = model.EmailNotificationsEnabled;
                }
                else if (isClientRole)
                {
                    user.CompanyName = model.CompanyName;
                    user.CompanyAddress = model.CompanyAddress;
                    user.CompanyType = model.CompanyType;
                    user.Designation = model.Designation;
                    user.MobileNumber = model.MobileNumber;
                    user.TinNumber = model.TinNumber;
                    user.EmailNotificationsEnabled = model.EmailNotificationsEnabled;
                }

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    try
                    {
                        await _userManager.AddToRoleAsync(user, NormalizeToIdentityRole(model.Role));
                        TempData["UserManagementMessage"] = $"{model.Role} account created successfully.";
                        return RedirectToAction(nameof(Users));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error adding role to user: {ex.Message}");
                        await _userManager.DeleteAsync(user); // Rollback user creation
                        ModelState.AddModelError("", "Error creating user account. Please try again.");
                    }
                }

                foreach (var error in result.Errors)
                {
                    _logger.LogError($"User creation error: {error.Description}");
                    ModelState.AddModelError("", error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error creating user: {ex.Message}");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
            }

            ViewBag.Role = model.Role;
            return View(model);
        }

        private async Task<bool> IsEmailInUseAsync(string email)
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            return existingUser != null;
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && !user.IsArchived)
            {
                user.IsActive = !user.IsActive;
                await _userManager.UpdateAsync(user);
                TempData["UserManagementMessage"] = $"User status updated to {(user.IsActive ? "active" : "inactive")}.";
            }
            return RedirectToAction(nameof(Users));
        }

        // Project Management
        public async Task<IActionResult> Projects()
        {
            var projects = await _context.Projects
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .Where(p => !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return View(projects);
        }

        [HttpGet]
        public async Task<IActionResult> CreateProject()
        {
            var clients = (await _userManager.GetUsersInRoleAsync("Client"))
                .Where(u => !u.IsArchived)
                .ToList();
            var projectManagers = (await GetUsersInRoleVariantsAsync("Project Manager"))
                .Where(u => !u.IsArchived)
                .ToList();

            var viewModel = new CreateProjectViewModel
            {
                Clients = clients.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = c.Id,
                    Text = $"{c.FirstName} {c.LastName}"
                }).ToList(),

                ProjectManagers = projectManagers.Select(pm => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = pm.Id,
                    Text = $"{pm.FirstName} {pm.LastName}"
                }).ToList()
            };

            // Provide tickets that are not yet assigned to any project so admin can optionally attach them
            // Only show tickets that are not assigned to a project and not assigned to an employee
            var unassignedTickets = await _context.Tickets
                .Where(t => t.ProjectId == null && string.IsNullOrEmpty(t.AssignedToId))
                .ToListAsync();

            viewModel.AvailableTickets = unassignedTickets.Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = t.Id.ToString(),
                Text = t.Title
            }).ToList();

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProject(CreateProjectViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Additional server-side validation for dates
                if (model.StartDate.Date < DateTime.Today)
                {
                    ModelState.AddModelError("StartDate", "Start date cannot be in the past");
                    return await PrepareCreateProjectViewModel(model);
                }

                if (model.EndDate < model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "End date must be after the start date");
                    return await PrepareCreateProjectViewModel(model);
                }

                var project = new Project
                {
                    Name = model.Name,
                    Description = model.Description,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    Status = model.Status,
                    ClientId = model.ClientId,
                    ProjectManagerId = model.ProjectManagerId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                // If tickets were selected, assign them to the newly created project
                if (model.SelectedTicketIds != null && model.SelectedTicketIds.Any())
                {
                    // Only assign tickets that are still unassigned (server-side validation)
                    var ticketsToAssign = await _context.Tickets
                        .Where(t => model.SelectedTicketIds.Contains(t.Id) && t.ProjectId == null && string.IsNullOrEmpty(t.AssignedToId))
                        .ToListAsync();

                    foreach (var ticket in ticketsToAssign)
                    {
                        ticket.ProjectId = project.Id;
                        ticket.UpdatedAt = DateTime.UtcNow;
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["ProjectMessage"] = "Project created successfully.";
                return RedirectToAction(nameof(Projects));
            }

            return await PrepareCreateProjectViewModel(model);
        }

        private async Task<IActionResult> PrepareCreateProjectViewModel(CreateProjectViewModel model)
        {
            var clients = (await _userManager.GetUsersInRoleAsync("Client"))
                .Where(u => !u.IsArchived)
                .ToList();
            var projectManagers = (await GetUsersInRoleVariantsAsync("Project Manager"))
                .Where(u => !u.IsArchived)
                .ToList();

            model.Clients = clients.Select(c => new SelectListItem
            {
                Value = c.Id,
                Text = $"{c.FirstName} {c.LastName}"
            }).ToList();

            model.ProjectManagers = projectManagers.Select(pm => new SelectListItem
            {
                Value = pm.Id,
                Text = $"{pm.FirstName} {pm.LastName}"
            }).ToList();

            // also populate available (unassigned) tickets for the view
            // Only show tickets that are not assigned to a project and not assigned to an employee
            var unassignedTickets = await _context.Tickets
                .Where(t => t.ProjectId == null && string.IsNullOrEmpty(t.AssignedToId))
                .ToListAsync();

            model.AvailableTickets = unassignedTickets.Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = t.Title
            }).ToList();

            return View(model);
        }

        // Task Management
        public async Task<IActionResult> Tasks(string filter)
        {
            var query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Where(t => !t.IsArchived)
                .AsQueryable();

            // Apply filters
            query = filter switch
            {
                "pending" => query.Where(t => !string.IsNullOrEmpty(t.ProofFilePath) && !t.IsConfirmed && t.Status != "Completed"),
                "completed" => query.Where(t => t.Status == "Completed"),
                "in-progress" => query.Where(t => t.Status == "In Progress"),
                _ => query
            };


            // Order: High priority first, then Medium/others, then Low last, then by UpdatedAt/CreatedAt
            var tasks = await query
                .OrderBy(t => t.Priority == "Low" ? 2 : t.Priority == "High" ? 0 : 1)
                .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .ToListAsync();

            ViewBag.CurrentFilter = filter;
            return View(tasks);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveTaskProof(int taskId)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                return NotFound();
            }

            if (task.IsArchived || task.Project.IsArchived)
            {
                TempData["Error"] = "Cannot approve a task that is archived or belongs to an archived project.";
                return RedirectToAction(nameof(Tasks));
            }

            if (string.IsNullOrEmpty(task.ProofFilePath))
            {
                TempData["Error"] = "No proof file has been submitted for this task.";
                return RedirectToAction(nameof(Tasks));
            }

            task.IsConfirmed = true;
            task.Status = "Completed";
            task.CompletedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;

            // Create notifications for both employee and project manager
            var notifications = new List<Notification>
            {
                // Notify the employee
                new Notification
                {
                    UserId = task.CreatedById,
                    Title = "Task Proof Approved by Admin",
                    Message = $"Your proof for task '{task.Title}' has been approved by admin",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    Link = $"/Employee/TaskDetails/{task.Id}"
                },
                // Notify the project manager
                new Notification
                {
                    UserId = task.Project.ProjectManagerId,
                    Title = "Task Proof Approved by Admin",
                    Message = $"Task proof for '{task.Title}' has been approved by admin",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    Link = $"/ProjectManager/TaskDetails/{task.Id}"
                }
            };

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Task proof has been approved and marked as completed.";
            return RedirectToAction(nameof(Tasks));
        }

        private async Task<IActionResult> PrepareCreateTaskViewModel(CreateTaskViewModel model)
        {
            // Get projects with their date constraints
            var projects = await _context.Projects
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.StartDate,
                    p.EndDate
                })
                .ToListAsync();

            var employees = await _userManager.GetUsersInRoleAsync("Employee");

            model.Projects = projects.Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Name
            }).ToList();

            model.Employees = employees.Select(e => new SelectListItem
            {
                Value = e.Id,
                Text = $"{e.FirstName} {e.LastName}"
            }).ToList();

            // Pass project data to view for JavaScript
            ViewBag.Projects = projects.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                startDate = p.StartDate.ToString("yyyy-MM-dd"),
                endDate = p.EndDate.HasValue ? p.EndDate.Value.ToString("yyyy-MM-dd") : p.StartDate.AddMonths(1).ToString("yyyy-MM-dd")
            });

            // Store employee data for dynamic filtering
            ViewBag.AllEmployees = await _context.Users
                .Where(u => u.Role == "Employee")
                .Select(u => new
                {
                    Id = u.Id,
                    FullName = $"{u.FirstName} {u.LastName}",
                    IncompleteTasks = _context.TaskAssignments
                        .Where(ta => ta.EmployeeId == u.Id)
                        .Join(_context.Tasks,
                            ta => ta.ProjectTaskId,
                            t => t.Id,
                            (ta, t) => new { ProjectId = t.ProjectId, Status = t.Status })
                        .Where(x => x.Status != "Completed")
                        .Select(x => x.ProjectId)
                        .ToList()
                })
                .ToListAsync();

            return View(model);
        }

        // Profile Management
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                EmailNotificationsEnabled = user.EmailNotificationsEnabled,

                ResidentialAddress = user.ResidentialAddress ?? string.Empty,
                Birthday = user.Birthday,
                PagIbigAccount = user.PagIbigAccount ?? string.Empty,
                MobileNumber = user.MobileNumber ?? string.Empty,
                Position = user.Position ?? string.Empty,

                IsActive = user.IsActive,
                IsArchived = user.IsArchived,
                ArchivedAt = user.ArchivedAt
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                var phoneAttr = new DoableFinal.Validation.PhonePHAttribute();
                var tinAttr = new DoableFinal.Validation.TinAttribute();
                var pagIbigAttr = new DoableFinal.Validation.PagIbigAttribute();

                bool isEmployeeRole = user.Role == "Employee" || user.Role == "Project Manager" || user.Role == "Admin";
                bool isClientRole = user.Role == "Client";

                // Validate required fields for Employee/Project Manager/Admin
                if (isEmployeeRole)
                {
                    if (string.IsNullOrWhiteSpace(model.ResidentialAddress))
                    {
                        ModelState.AddModelError("ResidentialAddress", "Residential Address is required.");
                        return View(model);
                    }

                    if (string.IsNullOrWhiteSpace(model.MobileNumber))
                    {
                        ModelState.AddModelError("MobileNumber", "Mobile Number is required.");
                        return View(model);
                    }

                    if (!model.Birthday.HasValue)
                    {
                        ModelState.AddModelError("Birthday", "Birthday is required.");
                        return View(model);
                    }

                    if (string.IsNullOrWhiteSpace(model.TinNumber))
                    {
                        ModelState.AddModelError("TinNumber", "TIN Number is required.");
                        return View(model);
                    }

                    if (string.IsNullOrWhiteSpace(model.PagIbigAccount))
                    {
                        ModelState.AddModelError("PagIbigAccount", "Pag-IBIG Account is required.");
                        return View(model);
                    }

                    if (string.IsNullOrWhiteSpace(model.Position))
                    {
                        ModelState.AddModelError("Position", "Position is required.");
                        return View(model);
                    }

                    // Validate format of required fields
                    if (!phoneAttr.IsValid(model.MobileNumber))
                    {
                        ModelState.AddModelError("MobileNumber", "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits).");
                        return View(model);
                    }

                    if (!tinAttr.IsValid(model.TinNumber))
                    {
                        ModelState.AddModelError("TinNumber", "Invalid TIN. Expected: XXX-XXX-XXX or XXX-XXX-XXX-XXX.");
                        return View(model);
                    }

                    if (!pagIbigAttr.IsValid(model.PagIbigAccount))
                    {
                        ModelState.AddModelError("PagIbigAccount", "Invalid Pag-IBIG MID. Expected 12 numeric digits.");
                        return View(model);
                    }
                }

                // Validate required fields for Client
                if (isClientRole)
                {
                    if (string.IsNullOrWhiteSpace(model.MobileNumber))
                    {
                        ModelState.AddModelError("MobileNumber", "Mobile Number is required.");
                        return View(model);
                    }

                    if (string.IsNullOrWhiteSpace(model.TinNumber))
                    {
                        ModelState.AddModelError("TinNumber", "TIN Number is required.");
                        return View(model);
                    }

                    // Validate format of required fields
                    if (!phoneAttr.IsValid(model.MobileNumber))
                    {
                        ModelState.AddModelError("MobileNumber", "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits).");
                        return View(model);
                    }

                    if (!tinAttr.IsValid(model.TinNumber))
                    {
                        ModelState.AddModelError("TinNumber", "Invalid TIN. Expected: XXX-XXX-XXX or XXX-XXX-XXX-XXX.");
                        return View(model);
                    }
                }

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.EmailNotificationsEnabled = model.EmailNotificationsEnabled;

                // Additional fields
                user.ResidentialAddress = model.ResidentialAddress;
                user.Birthday = model.Birthday;
                user.PagIbigAccount = model.PagIbigAccount;
                user.Position = model.Position;
                user.MobileNumber = model.MobileNumber;

                user.IsActive = model.IsActive;
                user.IsArchived = model.IsArchived;
                user.ArchivedAt = model.ArchivedAt;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["ProfileMessage"] = "Profile updated successfully.";
                    return RedirectToAction(nameof(Profile));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["PasswordErrorMessage"] = "Please check your input and try again.";
                return RedirectToAction(nameof(Profile));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Verify current password
            var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, model.CurrentPassword);
            if (!isCurrentPasswordValid)
            {
                TempData["PasswordErrorMessage"] = "Current password is incorrect.";
                return RedirectToAction(nameof(Profile));
            }

            // Change password
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                TempData["PasswordSuccessMessage"] = "Your password has been changed successfully.";
                // Sign in again to refresh the authentication cookie
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                TempData["PasswordErrorMessage"] = error.Description;
            }

            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleTwoFactor()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var currentStatus = await _userManager.GetTwoFactorEnabledAsync(user);
            await _userManager.SetTwoFactorEnabledAsync(user, !currentStatus);

            TempData["UserManagementMessage"] = $"Two-factor authentication has been {(!currentStatus ? "enabled" : "disabled")}.";
            return RedirectToAction(nameof(Profile));
        }

        // User Management
        [HttpGet]
        public async Task<IActionResult> UserDetails(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Role = user.Role,
                IsActive = user.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null)
                {
                    return NotFound();
                }

                // Update user's role first
                var currentRoles = await _userManager.GetRolesAsync(user);
                var normalizedRole = NormalizeToIdentityRole(model.Role);
                if (!currentRoles.Contains(normalizedRole))
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, normalizedRole);
                }

                // Update user properties
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email ?? string.Empty;
                user.IsActive = model.IsActive;
                user.Role = model.Role;  // Important: Update the Role property

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["UserManagementMessage"] = "User updated successfully.";
                    return RedirectToAction(nameof(Users));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["UserManagementMessage"] = "User deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete user.";
            }

            return RedirectToAction(nameof(Users));
        }

        // Project Management
        [HttpGet]
        public async Task<IActionResult> EditProject(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound();
            }



            if (project.IsArchived)
            {
                TempData["ErrorMessage"] = "Cannot edit an archived project. Reopen the project to make changes.";
                return RedirectToAction(nameof(ProjectDetails), new { id });
            }

            var model = new EditProjectViewModel
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Status = project.Status,
                ClientId = project.ClientId,
                ProjectManagerId = project.ProjectManagerId,
                ClientName = project.Client != null ? ($"{project.Client.FirstName} {project.Client.LastName}") : string.Empty,
                ProjectManagerName = project.ProjectManager != null ? ($"{project.ProjectManager.FirstName} {project.ProjectManager.LastName}") : string.Empty
            };

            ViewBag.Clients = await _context.Users.Where(u => u.Role == "Client").ToListAsync();
            ViewBag.ProjectManagers = await _context.Users.Where(u => u.Role == "Project Manager").ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProject(EditProjectViewModel model)
        {
            if (ModelState.IsValid)
            {
                var project = await _context.Projects.FindAsync(model.Id);
                if (project == null)
                {
                    return NotFound();
                }

                if (project.IsArchived)
                {
                    TempData["ErrorMessage"] = "Cannot edit an archived project. Please unarchive the project to make changes.";
                    return RedirectToAction(nameof(ProjectDetails), new { id = project.Id });
                }

                // Validate start date is not in the past
                if (model.StartDate.Date < DateTime.Today)
                {
                    ModelState.AddModelError("StartDate", "Start date cannot be in the past");
                    ViewBag.Clients = await _context.Users.Where(u => u.Role == "Client").ToListAsync();
                    ViewBag.ProjectManagers = await _context.Users.Where(u => u.Role == "Project Manager").ToListAsync();
                    return View(model);
                }

                // If project is already started (In Progress/Completed), don't allow changing start date to future date
                if (project.Status != "Not Started" && model.StartDate.Date > project.StartDate.Date)
                {
                    ModelState.AddModelError("StartDate", "Cannot change start date for a project that has already started");
                    ViewBag.Clients = await _context.Users.Where(u => u.Role == "Client").ToListAsync();
                    ViewBag.ProjectManagers = await _context.Users.Where(u => u.Role == "Project Manager").ToListAsync();
                    return View(model);
                }

                var oldStatus = project.Status;
                project.Name = model.Name;
                project.Description = model.Description;
                project.StartDate = model.StartDate;
                project.EndDate = model.EndDate;
                project.Status = model.Status;
                project.ClientId = model.ClientId ?? project.ClientId;
                project.ProjectManagerId = model.ProjectManagerId ?? project.ProjectManagerId;
                project.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Send notification if status changed
                if (oldStatus != project.Status)
                {
                    await _notificationService.NotifyProjectUpdateAsync(project, $"Project status updated from {oldStatus} to {project.Status}");
                }

                TempData["ProjectMessage"] = "Project updated successfully.";
                return RedirectToAction(nameof(Projects));
            }

            ViewBag.Clients = await _context.Users.Where(u => u.Role == "Client").ToListAsync();
            ViewBag.ProjectManagers = await _context.Users.Where(u => u.Role == "Project Manager").ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveProject(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            // Check if project can be archived
            if (project.Status == "In Progress")
            {
                TempData["ErrorMessage"] = "Cannot archive an ongoing project. Project must be completed or not started.";
                return RedirectToAction(nameof(ProjectDetails), new { id });
            }

            // Archive the project
            project.IsArchived = true;
            project.ArchivedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;

            // Archive all tasks in the project
            foreach (var task in project.Tasks)
            {
                task.IsArchived = true;
                task.ArchivedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Notify relevant parties
            await _notificationService.NotifyProjectUpdateAsync(project, $"Project '{project.Name}' has been archived");

            TempData["ProjectMessage"] = "Project has been archived successfully.";
            return RedirectToAction(nameof(Projects));
        }

        public async Task<IActionResult> ArchivedProjects()
        {
            var projects = await _context.Projects
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .Where(p => p.IsArchived)
                .OrderByDescending(p => p.ArchivedAt)
                .ToListAsync();

            return View(projects);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnarchiveProject(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            // Unarchive the project
            project.IsArchived = false;
            project.ArchivedAt = null;
            // Reopen the project
            project.Status = "In Progress";
            project.UpdatedAt = DateTime.UtcNow;

            // Unarchive all tasks in the project
            foreach (var task in project.Tasks)
            {
                task.IsArchived = false;
                task.ArchivedAt = null;
                task.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Notify relevant parties
            await _notificationService.NotifyProjectUpdateAsync(project, $"Project '{project.Name}' has been unarchived by admin");

            TempData["ProjectMessage"] = "Project has been reopened and unarchived.";

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = true,
                    status = project.Status,
                    isArchived = project.IsArchived
                });
            }

            return RedirectToAction(nameof(ArchivedProjects));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnarchiveTask(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            // Unarchive the task
            task.IsArchived = false;
            task.ArchivedAt = null;
            task.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify relevant parties
            await _notificationService.NotifyTaskUpdateAsync(task, $"Task '{task.Title}' has been unarchived by admin");

            TempData["TaskMessage"] = "Task has been unarchived successfully.";
            return RedirectToAction(nameof(ArchivedTasks));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProject(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Tasks)
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            // Verify all tasks are completed
            var totalTasks = project.Tasks?.Count ?? 0;
            var completedTasks = project.Tasks?.Count(t => t.Status == "Completed") ?? 0;
            
            if (totalTasks == 0 || completedTasks != totalTasks)
            {
                TempData["ErrorMessage"] = "Cannot complete project: Not all tasks are completed.";
                return RedirectToAction(nameof(ProjectDetails), new { id });
            }

            // Update project status and archive project
            var oldStatus = project.Status;
            project.Status = "Completed";
            project.IsArchived = true;
            project.ArchivedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;

            // Archive project tasks
            foreach (var task in project.Tasks)
            {
                task.IsArchived = true;
                task.ArchivedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Notify relevant parties
            await _notificationService.NotifyProjectUpdateAsync(project, $"Project '{project.Name}' has been marked as completed");

            TempData["ProjectMessage"] = "Project has been marked as completed and archived.";

            // Return JSON for AJAX
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = true,
                    status = project.Status,
                    isArchived = project.IsArchived,
                    archivedAt = project.ArchivedAt?.ToString("o")
                });
            }

            return RedirectToAction(nameof(ProjectDetails), new { id });
        }

        // Task Management
        [HttpGet]
        public async Task<IActionResult> EditTask(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            if (task.IsArchived || task.Project.IsArchived)
            {
                TempData["ErrorMessage"] = "Cannot edit an archived task or a task under an archived project.";
                return RedirectToAction(nameof(TaskDetails), new { id = task.Id });
            }

            var model = new EditTaskViewModel
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                Status = task.Status,
                Priority = task.Priority,
                ProjectId = task.ProjectId,
                // Get list of assigned employees
                AssignedToIds = task.TaskAssignments.Select(ta => ta.EmployeeId).ToList()
            };

            ViewBag.Projects = await _context.Projects.ToListAsync();
            ViewBag.Employees = await _context.Users.Where(u => u.Role == "Employee").ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTask(EditTaskViewModel model)
        {
            if (ModelState.IsValid)
            {
                var task = await _context.Tasks
                    .Include(t => t.TaskAssignments)
                    .FirstOrDefaultAsync(t => t.Id == model.Id);

                if (task == null)
                {
                    return NotFound();
                }

                var oldStatus = task.Status;
                task.Title = model.Title;
                task.Description = model.Description;
                task.StartDate = model.StartDate;
                task.DueDate = model.DueDate;
                task.Status = model.Status;
                task.Priority = model.Priority;
                task.ProjectId = model.ProjectId;
                task.UpdatedAt = DateTime.UtcNow;

                // Handle task assignments - first remove existing assignments
                var currentAssignments = await _context.TaskAssignments
                    .Where(ta => ta.ProjectTaskId == task.Id)
                    .ToListAsync();

                _context.TaskAssignments.RemoveRange(currentAssignments);
                await _context.SaveChangesAsync();

                // Add new assignments
                if (model.AssignedToIds != null && model.AssignedToIds.Any())
                {
                    foreach (var employeeId in model.AssignedToIds)
                    {
                        // Check if employee is already in project team
                        var isInTeam = await _context.ProjectTeams
                            .AnyAsync(pt => pt.ProjectId == model.ProjectId && pt.UserId == employeeId);

                        // If not in team, add them
                        if (!isInTeam)
                        {
                            var projectTeam = new ProjectTeam
                            {
                                ProjectId = model.ProjectId,
                                UserId = employeeId,
                                Role = "Team Member",
                                JoinedAt = DateTime.UtcNow
                            };
                            _context.ProjectTeams.Add(projectTeam);
                        }

                        // Create new task assignment
                        var taskAssignment = new TaskAssignment
                        {
                            ProjectTaskId = task.Id,
                            EmployeeId = employeeId,
                            AssignedAt = DateTime.UtcNow
                        };
                        _context.TaskAssignments.Add(taskAssignment);
                    }
                }

                await _context.SaveChangesAsync();

                // Send notification if status changed
                if (oldStatus != task.Status)
                {
                    await _notificationService.NotifyTaskUpdateAsync(task, $"Task status updated from {oldStatus} to {task.Status}");
                }

                TempData["TaskMessage"] = "Task updated successfully.";
                return RedirectToAction(nameof(Tasks));
            }

            ViewBag.Projects = await _context.Projects.ToListAsync();
            ViewBag.Employees = await _context.Users.Where(u => u.Role == "Employee").ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveTask(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            // Check if task can be archived
            if (task.Status == "In Progress")
            {
                TempData["ErrorMessage"] = "Cannot archive an ongoing task. Task must be completed or not started.";
                return RedirectToAction(nameof(TaskDetails), new { id });
            }

            // Archive the task
            task.IsArchived = true;
            task.ArchivedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify relevant parties
            await _notificationService.NotifyTaskUpdateAsync(task, $"Task '{task.Title}' has been archived");

            TempData["TaskMessage"] = "Task has been archived successfully.";
            return RedirectToAction(nameof(Tasks));
        }

        public async Task<IActionResult> ArchivedTasks()
        {
            var tasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Where(t => t.IsArchived)
                .OrderByDescending(t => t.ArchivedAt)
                .ToListAsync();

            return View(tasks);
        }        public async Task<IActionResult> TaskDetails(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Include(t => t.Comments)
                .Include(t => t.Attachments)
                .FirstOrDefaultAsync(t => t.Id == id);

                if (task == null)
            {
                return NotFound();
            }

                if (task.IsArchived || task.Project.IsArchived)
                {
                    TempData["ErrorMessage"] = "Cannot edit an archived task or a task under an archived project.";
                    return RedirectToAction(nameof(TaskDetails), new { id = task.Id });
                }

            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int taskId, string commentText)
        {
            if (string.IsNullOrWhiteSpace(commentText))
            {
                TempData["ErrorMessage"] = "Comment text cannot be empty.";
                return RedirectToAction("TaskDetails", new { id = taskId });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                TempData["ErrorMessage"] = "You must be logged in to post a comment.";
                return RedirectToAction("TaskDetails", new { id = taskId });
            }

            var task = await _context.Tasks
                .Include(t => t.Project)
                    .ThenInclude(p => p.ProjectTeams)
                .Include(t => t.TaskAssignments)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                TempData["ErrorMessage"] = "Task not found.";
                return RedirectToAction("Tasks");
            }

            // Initialize ProjectTeams if null
            if (task.Project.ProjectTeams == null)
            {
                task.Project.ProjectTeams = new List<ProjectTeam>();
            }

            // Check if the user is authorized to comment
            var isUserInProject = task.Project.ProjectTeams.Any(pt => pt.UserId == currentUser.Id);
            var isUserAssignedToTask = task.TaskAssignments.Any(ta => ta.EmployeeId == currentUser.Id);
            var isUserProjectManager = task.Project.ProjectManagerId == currentUser.Id;
            var isUserClient = task.Project.ClientId == currentUser.Id;
            var isUserAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            if (!isUserInProject && !isUserAssignedToTask && !isUserProjectManager && !isUserClient && !isUserAdmin)
            {
                TempData["ErrorMessage"] = "You are not authorized to comment on this task.";
                return RedirectToAction("TaskDetails", new { id = taskId });
            }

            var comment = new TaskComment
            {
                ProjectTaskId = taskId,
                CommentText = commentText,
                CreatedById = currentUser.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.TaskComments.Add(comment);
            await _context.SaveChangesAsync();

            TempData["TaskMessage"] = "Comment posted successfully.";
            return RedirectToAction("TaskDetails", new { id = taskId });
        }

        [HttpGet]
        public async Task<IActionResult> ProjectDetails(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .Include(p => p.Tasks)
                    .ThenInclude(t => t.TaskAssignments)
                .Include(p => p.ProjectTeams)
                    .ThenInclude(pt => pt.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound(); // Handle the case where the project is not found
            }

            return View(project);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmTask(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            // Only allow confirmation if task is in "For Review" status
            if (task.Status != "For Review")
            {
                TempData["ErrorMessage"] = "Task must be in 'For Review' status to be confirmed.";
                return RedirectToAction(nameof(TaskDetails), new { id });
            }

            task.Status = "Completed";
            task.CompletedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["TaskMessage"] = "Task has been confirmed as completed.";
            return RedirectToAction(nameof(TaskDetails), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> CreateTask()
        {
            // Get all active projects
            var projects = await _context.Projects
                .Where(p => !p.IsArchived)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.StartDate,
                    p.EndDate
                })
                .ToListAsync();

            // Get all active employees
            var employees = (await _userManager.GetUsersInRoleAsync("Employee"))
                .Where(u => !u.IsArchived)
                .ToList();
            
            // Get task assignments including incomplete tasks for each employee
            var projectTaskAssignments = await _context.TaskAssignments
                .Include(ta => ta.ProjectTask)
                .Select(ta => new { 
                    ta.EmployeeId, 
                    ta.ProjectTask.ProjectId,
                    IsCompleted = ta.ProjectTask.Status == "Completed"
                })
                .ToListAsync();

            // Group task assignments by employee to find who has incomplete tasks
            var employeesWithIncompleteTasks = projectTaskAssignments
                .Where(ta => !ta.IsCompleted)
                .GroupBy(ta => ta.EmployeeId)
                .ToDictionary(g => g.Key, g => g.Select(ta => ta.ProjectId).Distinct().ToList());

            var model = new CreateTaskViewModel
            {
                Projects = projects.Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                }).ToList(),
                AvailableEmployees = employees.Select(e => new
                {
                    id = e.Id,
                    text = $"{e.FirstName} {e.LastName} ({e.Email})",
                    projectAssignments = projectTaskAssignments
                        .Where(pta => pta.EmployeeId == e.Id)
                        .Select(pta => pta.ProjectId)
                        .Distinct()
                        .ToList(),
                    incompleteTaskProjects = employeesWithIncompleteTasks.ContainsKey(e.Id) 
                        ? employeesWithIncompleteTasks[e.Id] 
                        : new List<int>()
                }).ToList()
            };

            // Pass project dates and employee data to the view
            var projectDatesJson = projects.ToDictionary(
                p => p.Id,
                p => new { start = p.StartDate.ToString("yyyy-MM-dd"), end = p.EndDate?.ToString("yyyy-MM-dd") }
            );
            ViewBag.ProjectDatesJson = System.Text.Json.JsonSerializer.Serialize(projectDatesJson);
            ViewBag.EmployeesJson = System.Text.Json.JsonSerializer.Serialize(model.AvailableEmployees);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTask(CreateTaskViewModel model)
        {
            if (!ModelState.IsValid)
            {
                foreach (var modelStateKey in ModelState.Keys)
                {
                    var modelStateVal = ModelState[modelStateKey];                    if (modelStateVal?.Errors != null)
                    {
                        foreach (var error in modelStateVal.Errors)
                        {
                            _logger.LogError($"Validation error for {modelStateKey}: {error.ErrorMessage}");
                        }
                    }
                }
                TempData["ErrorMessage"] = "There were validation errors. Please check the form and try again.";
                return await PrepareCreateTaskViewModel(model);
            }

        

            // Get project dates
            var project = await _context.Projects.FindAsync(model.ProjectId);
            if (project == null)
            {
                ModelState.AddModelError("ProjectId", "Invalid project selected");
                return await PrepareCreateTaskViewModel(model);
            }

            if (project.IsArchived)
            {
                ModelState.AddModelError(string.Empty, "Cannot create a task on an archived project.");
                return await PrepareCreateTaskViewModel(model);
            }

            // Validate task dates against project dates
            if (model.StartDate < project.StartDate)
            {
                ModelState.AddModelError("StartDate", "Task cannot start before the project start date");
                return await PrepareCreateTaskViewModel(model);
            }

            if (project.EndDate.HasValue && model.DueDate > project.EndDate.Value)
            {
                ModelState.AddModelError("DueDate", "Task cannot end after the project end date");
                return await PrepareCreateTaskViewModel(model);
            }

            if (model.StartDate > model.DueDate)
            {
                ModelState.AddModelError("DueDate", "Due date must be after the start date");
                return await PrepareCreateTaskViewModel(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var task = new ProjectTask
                {
                    Title = model.Title,
                    Description = model.Description,
                    StartDate = model.StartDate,
                    DueDate = model.DueDate,
                    Status = model.Status,
                    Priority = model.Priority,
                    ProjectId = model.ProjectId,
                    CreatedById = currentUser.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();

                // Add assignments if specified
                if (model.AssignedToIds != null && model.AssignedToIds.Any())
                {
                    foreach (var employeeId in model.AssignedToIds)
                    {
                        // Check if employee is already in project team
                        var isInTeam = await _context.ProjectTeams
                            .AnyAsync(pt => pt.ProjectId == model.ProjectId && pt.UserId == employeeId);

                        // If not in team, add them
                        if (!isInTeam)
                        {
                            var projectTeam = new ProjectTeam
                            {
                                ProjectId = model.ProjectId,
                                UserId = employeeId,
                                Role = "Team Member",
                                JoinedAt = DateTime.UtcNow
                            };
                            _context.ProjectTeams.Add(projectTeam);
                        }

                        var taskAssignment = new TaskAssignment
                        {
                            ProjectTaskId = task.Id,
                            EmployeeId = employeeId,
                            AssignedAt = DateTime.UtcNow
                        };
                        _context.TaskAssignments.Add(taskAssignment);
                    }

                    await _context.SaveChangesAsync();
                }

                await _notificationService.NotifyTaskUpdateAsync(task, $"New task '{task.Title}' has been created");

                TempData["TaskMessage"] = "Task created successfully.";
                return RedirectToAction(nameof(Tasks));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating task: {ex.Message}");
                ModelState.AddModelError("", "An error occurred while creating the task. Please try again.");
                return await PrepareCreateTaskViewModel(model);
            }
        }

        // --- Ticket management for Admins ---
        public async Task<IActionResult> Tickets(string statusFilter = "")
        {
            var query = _context.Tickets
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.CreatedBy)
                // include tickets with no project as well as tickets whose project is not archived
                .Where(t => t.Project == null || !t.Project.IsArchived)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(t => t.Status == statusFilter);
            }

            var tickets = await query
                .OrderByDescending(t => t.Priority == "Critical" ? 4 : t.Priority == "High" ? 3 : t.Priority == "Medium" ? 2 : t.Priority == "Low" ? 1 : 0)
                .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .ToListAsync();

            ViewBag.StatusFilter = statusFilter;
            ViewBag.AvailableStatuses = new List<string> { "Open", "In Progress", "Resolved", "Closed" };
            return View(tickets);
        }

        [HttpGet]
        public async Task<IActionResult> EditTicket(int id)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.CreatedBy)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }

            var model = new ViewModels.TicketStatusEditViewModel
            {
                Id = ticket.Id,
                Title = ticket.Title,
                CurrentStatus = ticket.Status,
                AvailableStatuses = new List<SelectListItem>
                {
                    new SelectListItem("Open", "Open"),
                    new SelectListItem("In Progress", "In Progress"),
                    new SelectListItem("Resolved", "Resolved"),
                    new SelectListItem("Closed", "Closed")
                }
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTicket(ViewModels.TicketStatusEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableStatuses = new List<SelectListItem>
                {
                    new SelectListItem("Open", "Open"),
                    new SelectListItem("In Progress", "In Progress"),
                    new SelectListItem("Resolved", "Resolved"),
                    new SelectListItem("Closed", "Closed")
                };
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(s => !string.IsNullOrWhiteSpace(s));
                TempData["TicketMessage"] = "Status update failed: " + string.Join("; ", errors);
                return View(model);
            }

            var ticket = await _context.Tickets.FindAsync(model.Id);
            if (ticket == null)
            {
                return NotFound();
            }

            var oldStatus = ticket.Status;
            ticket.Status = model.CurrentStatus;
            ticket.UpdatedAt = DateTime.UtcNow;
            if (ticket.Status == "Resolved")
            {
                ticket.ResolvedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            if (oldStatus != ticket.Status)
            {
                // Notify ticket owner and assignee if status changed
                var recipients = new List<string>();
                if (!string.IsNullOrEmpty(ticket.CreatedById)) recipients.Add(ticket.CreatedById);
                if (!string.IsNullOrEmpty(ticket.AssignedToId)) recipients.Add(ticket.AssignedToId);

                foreach (var userId in recipients.Distinct())
                {
                    var notification = new Notification
                    {
                        UserId = userId,
                        Title = "Ticket Status Updated",
                        Message = $"Ticket '{ticket.Title}' status changed from {oldStatus} to {ticket.Status}.",
                        Link = $"/Admin/EditTicket/{ticket.Id}",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false,
                        Type = NotificationType.General
                    };
                    _context.Notifications.Add(notification);
                }
                await _context.SaveChangesAsync();
            }

            TempData["TicketMessage"] = "Ticket status updated successfully.";
            return RedirectToAction(nameof(Tickets));
        }

        // Simplified POST handler to support button-based status updates from the TicketDetails view
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTicketStatus(int Id, string CurrentStatus)
        {
            var ticket = await _context.Tickets.FindAsync(Id);
            if (ticket == null)
            {
                return NotFound();
            }

            var oldStatus = ticket.Status;
            ticket.Status = CurrentStatus;
            ticket.UpdatedAt = DateTime.UtcNow;
            if (ticket.Status == "Resolved")
            {
                ticket.ResolvedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            if (oldStatus != ticket.Status)
            {
                var recipients = new List<string>();
                if (!string.IsNullOrEmpty(ticket.CreatedById)) recipients.Add(ticket.CreatedById);
                if (!string.IsNullOrEmpty(ticket.AssignedToId)) recipients.Add(ticket.AssignedToId);
                    foreach (var userId in recipients.Distinct())
                    {
                        // Use the public ticket details route by default. Client JS or views will remap accordingly.
                        var notification = new Notification
                        {
                            UserId = userId,
                            Title = "Ticket Status Updated",
                            Message = $"Ticket '{ticket.Title}' status changed from {oldStatus} to {ticket.Status}.",
                            Link = $"/Ticket/Details/{ticket.Id}",
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false,
                            Type = NotificationType.General
                        };
                        _context.Notifications.Add(notification);
                    }
                await _context.SaveChangesAsync();
            }

            TempData["TicketMessage"] = "Ticket status updated successfully.";
            return RedirectToAction("TicketDetails", new { id = Id });
        }

        public async Task<IActionResult> TicketDetails(int id)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Include(t => t.CreatedBy)
                .Include(t => t.Comments.OrderByDescending(c => c.CreatedAt))
                    .ThenInclude(c => c.CreatedBy)
                .Include(t => t.Attachments.OrderByDescending(a => a.UploadedAt))
                    .ThenInclude(a => a.UploadedBy)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }

            var viewModel = new TicketDetailsViewModel
            {
                Ticket = ticket,
                Comments = ticket.Comments?.ToList() ?? new List<TicketComment>(),
                Attachments = ticket.Attachments?.ToList() ?? new List<TicketAttachment>()
            };

            return View(viewModel);
        }

        // --- Content Management for public pages ---
        private readonly string _contentFile = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "home_pages.json");

        private IDictionary<string, ViewModels.ContentPageViewModel> LoadContentPages()
        {
            try
            {
                if (!System.IO.File.Exists(_contentFile))
                {
                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(_contentFile));

                    var defaultPages = new Dictionary<string, ViewModels.ContentPageViewModel>
                    {
                        { "Index", new ViewModels.ContentPageViewModel { Key = "Index", DisplayName = "Home - Index", TitleHtml = "<h1>QONNEC</h1>", BodyHtml = "<p>Streamline your projects...</p>", ImagePath = "" } },
                        { "About", new ViewModels.ContentPageViewModel { Key = "About", DisplayName = "Home - About", TitleHtml = "<h1>About Us</h1>", BodyHtml = "<p>About content...</p>", ImagePath = "" } },
                        { "Services", new ViewModels.ContentPageViewModel { Key = "Services", DisplayName = "Home - Services", TitleHtml = "<h1>Our Services</h1>", BodyHtml = "<p>Services...</p>", ImagePath = "" } },
                        { "Contact", new ViewModels.ContentPageViewModel { Key = "Contact", DisplayName = "Home - Contact", TitleHtml = "<h1>Contact</h1>", BodyHtml = "<p>Contact form text...</p>", ImagePath = "" } }
                    };
                    var json = System.Text.Json.JsonSerializer.Serialize(defaultPages, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(_contentFile, json);
                }

                var contentJson = System.IO.File.ReadAllText(_contentFile);
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ViewModels.ContentPageViewModel>>(contentJson) ?? new Dictionary<string, ViewModels.ContentPageViewModel>();
                return dict.ToDictionary(k => k.Key, v => v.Value);
            }
            catch
            {
                return new Dictionary<string, ViewModels.ContentPageViewModel>();
            }
        }

        private void SaveContentPages(IDictionary<string, ViewModels.ContentPageViewModel> pages)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_contentFile));
            var json = System.Text.Json.JsonSerializer.Serialize(pages, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(_contentFile, json);
        }

        [HttpGet]
        public IActionResult ContentMS()
        {
            var pages = LoadContentPages().Values.ToList();
            return View(pages);
        }

        [HttpGet]
        public IActionResult EditContent(string key)
        {
            if (string.IsNullOrEmpty(key)) return RedirectToAction(nameof(ContentMS));
            var pages = LoadContentPages();
            if (!pages.ContainsKey(key)) return RedirectToAction(nameof(ContentMS));
            return View(pages[key]);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveContent(string key, string titleHtml, string bodyHtml, IFormFile imageFile)
        {
            if (string.IsNullOrEmpty(key)) return RedirectToAction(nameof(ContentMS));

            var pages = LoadContentPages();
            if (!pages.ContainsKey(key))
            {
                pages[key] = new ViewModels.ContentPageViewModel { Key = key, DisplayName = key };
            }

            var page = pages[key];
            page.TitleHtml = titleHtml ?? string.Empty;
            page.BodyHtml = bodyHtml ?? string.Empty;

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "content", key);
                Directory.CreateDirectory(uploadDir);
                var fileName = $"{DateTime.UtcNow.Ticks}_{Path.GetFileName(imageFile.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);
                using (var stream = System.IO.File.Create(filePath))
                {
                    await imageFile.CopyToAsync(stream);
                }
                // store web path
                page.ImagePath = $"/uploads/content/{key}/{fileName}";
            }

            pages[key] = page;
            SaveContentPages(pages);

            TempData["ContentMessage"] = "Content saved successfully.";
            if (!string.IsNullOrEmpty(page.ImagePath)) TempData["ImagePath"] = page.ImagePath;
            return RedirectToAction(nameof(EditContent), new { key = key });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTicketComment(int ticketId, string commentText)
        {
            if (string.IsNullOrWhiteSpace(commentText))
            {
                TempData["ErrorMessage"] = "Comment text cannot be empty.";
                return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                TempData["ErrorMessage"] = "You must be logged in to post a comment.";
                return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
            }

            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Ticket not found.";
                return RedirectToAction(nameof(Tickets));
            }

            var comment = new TicketComment
            {
                TicketId = ticketId,
                CommentText = commentText,
                CreatedById = currentUser.Id,
                CreatedAt = DateTime.Now
            };

            _context.TicketComments.Add(comment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Comment added successfully.";
            return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAttachment(int ticketId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a file to upload.";
                return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
            }

            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            // Create uploads directory if it doesn't exist, and save file to disk
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "tickets", ticketId.ToString());
            Directory.CreateDirectory(uploadsFolder);

            // Generate a unique filename and write the file to disk
            var fileName = Path.GetFileName(file.FileName);
            var uniqueFileName = $"{DateTime.Now.Ticks}_{fileName}";
            var savedFilePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var stream = System.IO.File.Create(savedFilePath))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new TicketAttachment
            {
                TicketId = ticketId,
                FilePath = $"/uploads/tickets/{ticketId}/{uniqueFileName}",
                FileName = fileName,
                FileType = file.ContentType,
                FileSize = file.Length,
                UploadedById = currentUser.Id,
                UploadedAt = DateTime.UtcNow
            };

            _context.TicketAttachments.Add(attachment);
            await _context.SaveChangesAsync();

            // Notify the ticket creator if they're not the one uploading
            if (ticket.CreatedById != currentUser.Id)
            {
                // Use the public Ticket/Details route in the notification so clients can view the ticket
                await _notificationService.CreateNotification(
                    ticket.CreatedById,
                    "New Attachment on Your Ticket",
                    $"An admin has added an attachment to your ticket: {ticket.Title}",
                    $"/Ticket/Details/{ticketId}"
                );
            }

            return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
        }

        // ===== HOMEPAGE CMS MANAGEMENT =====
        
        [HttpGet]
        public async Task<IActionResult> ManageHomePage()
        {
            var sections = await _homePageService.GetAllSectionsAsync();
            return View(sections);
        }

        [HttpGet]
        public async Task<IActionResult> EditHomePageSection(int id)
        {
            var section = await _context.HomePageSections.FindAsync(id);
            if (section == null)
                return NotFound();

            // Determine if the section allows image upload
            ViewBag.ImageUploadAllowed = _imageSectionKeys.Contains(section.SectionKey ?? string.Empty);
            // If an ImagePath was just saved, show it immediately even if the db has not been refreshed yet
            if (!string.IsNullOrEmpty(Convert.ToString(TempData["ImagePath"])) && string.IsNullOrEmpty(section.ImagePath))
            {
                section.ImagePath = Convert.ToString(TempData["ImagePath"]);
            }
            return View(section);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditHomePageSection(int id, string content, IFormFile? imageFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            string? imagePath = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                var section = await _context.HomePageSections.FindAsync(id);
                if (section != null)
                {
                    var key = section.SectionKey ?? "home";
                    var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "home", key);
                    Directory.CreateDirectory(uploadDir);
                    var fileName = $"{DateTime.UtcNow.Ticks}_{Path.GetFileName(imageFile.FileName)}";
                    var filePath = Path.Combine(uploadDir, fileName);
                    using (var stream = System.IO.File.Create(filePath))
                    {
                        await imageFile.CopyToAsync(stream);
                    }
                    imagePath = $"/uploads/home/{key}/{fileName}";
                }
            }
            await _homePageService.UpdateSectionAsync(id, content, user.Id, imagePath);
            TempData["SuccessMessage"] = "Section updated successfully!";
            if (!string.IsNullOrEmpty(imagePath)) TempData["ImagePath"] = imagePath;
            return RedirectToAction(nameof(EditHomePageSection), new { id });
        }

        // ===== ABOUT PAGE CMS MANAGEMENT =====
        
        [HttpGet]
        public async Task<IActionResult> ManageAboutPage()
        {
            var sections = await _homePageService.GetAllSectionsAsync();
            // Filter only About page sections
            var aboutSections = sections.Where(s => s.SectionKey.StartsWith("about-")).ToList();
            return View(aboutSections);
        }

        [HttpGet]
        public async Task<IActionResult> EditAboutPageSection(int id)
        {
            var section = await _context.HomePageSections.FindAsync(id);
            if (section == null)
                return NotFound();
            
            // Verify it's an About section
            if (!section.SectionKey.StartsWith("about-"))
                return Unauthorized();

            ViewBag.ImageUploadAllowed = _imageSectionKeys.Contains(section.SectionKey ?? string.Empty);
            if (!string.IsNullOrEmpty(Convert.ToString(TempData["ImagePath"])) && string.IsNullOrEmpty(section.ImagePath))
            {
                section.ImagePath = Convert.ToString(TempData["ImagePath"]);
            }
            return View(section);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAboutPageSection(int id, string content, IFormFile? imageFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var section = await _context.HomePageSections.FindAsync(id);
            if (section == null || !section.SectionKey.StartsWith("about-"))
                return Unauthorized();

            string? imagePath = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                if (section != null)
                {
                    var key = section.SectionKey ?? "about";
                    var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "home", key);
                    Directory.CreateDirectory(uploadDir);
                    var fileName = $"{DateTime.UtcNow.Ticks}_{Path.GetFileName(imageFile.FileName)}";
                    var filePath = Path.Combine(uploadDir, fileName);
                    using (var stream = System.IO.File.Create(filePath))
                    {
                        await imageFile.CopyToAsync(stream);
                    }
                    imagePath = $"/uploads/home/{key}/{fileName}";
                }
            }
            await _homePageService.UpdateSectionAsync(id, content, user.Id, imagePath);
            TempData["SuccessMessage"] = "Section updated successfully!";
            if (!string.IsNullOrEmpty(imagePath)) TempData["ImagePath"] = imagePath;
            return RedirectToAction(nameof(EditAboutPageSection), new { id });
        }

        // ===== SERVICES PAGE CMS MANAGEMENT =====
        
        [HttpGet]
        public async Task<IActionResult> ManageServicesPage()
        {
            var sections = await _homePageService.GetAllSectionsAsync();
            // Filter only Services page sections
            var servicesSections = sections.Where(s => s.SectionKey.StartsWith("services-")).ToList();
            return View(servicesSections);
        }

        [HttpGet]
        public async Task<IActionResult> EditServicesPageSection(int id)
        {
            var section = await _context.HomePageSections.FindAsync(id);
            if (section == null)
                return NotFound();
            
            // Verify it's a Services section
            if (!section.SectionKey.StartsWith("services-"))
                return Unauthorized();

            ViewBag.ImageUploadAllowed = _imageSectionKeys.Contains(section.SectionKey ?? string.Empty);
            if (!string.IsNullOrEmpty(Convert.ToString(TempData["ImagePath"])) && string.IsNullOrEmpty(section.ImagePath))
            {
                section.ImagePath = Convert.ToString(TempData["ImagePath"]);
            }
            return View(section);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditServicesPageSection(int id, string content, IFormFile? imageFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var section = await _context.HomePageSections.FindAsync(id);
            if (section == null || !section.SectionKey.StartsWith("services-"))
                return Unauthorized();

            string? imagePath = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                if (section != null)
                {
                    var key = section.SectionKey ?? "services";
                    var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "home", key);
                    Directory.CreateDirectory(uploadDir);
                    var fileName = $"{DateTime.UtcNow.Ticks}_{Path.GetFileName(imageFile.FileName)}";
                    var filePath = Path.Combine(uploadDir, fileName);
                    using (var stream = System.IO.File.Create(filePath))
                    {
                        await imageFile.CopyToAsync(stream);
                    }
                    imagePath = $"/uploads/home/{key}/{fileName}";
                }
            }
            await _homePageService.UpdateSectionAsync(id, content, user.Id, imagePath);
            TempData["SuccessMessage"] = "Section updated successfully!";
            if (!string.IsNullOrEmpty(imagePath)) TempData["ImagePath"] = imagePath;
            return RedirectToAction(nameof(EditServicesPageSection), new { id });
        }

        public async Task<IActionResult> ManageContactPage()
        {
            var sections = await _homePageService.GetAllSectionsAsync();
            // Filter only Contact page sections
            var contactSections = sections.Where(s => s.SectionKey.StartsWith("contact-")).ToList();
            return View(contactSections);
        }

        [HttpGet]
        public async Task<IActionResult> EditContactPageSection(int id)
        {
            var section = await _context.HomePageSections.FindAsync(id);
            if (section == null)
                return NotFound();
            
            // Verify it's a Contact section
            if (!section.SectionKey.StartsWith("contact-"))
                return Unauthorized();

            ViewBag.ImageUploadAllowed = _imageSectionKeys.Contains(section.SectionKey ?? string.Empty);
            if (!string.IsNullOrEmpty(Convert.ToString(TempData["ImagePath"])) && string.IsNullOrEmpty(section.ImagePath))
            {
                section.ImagePath = Convert.ToString(TempData["ImagePath"]);
            }
            return View(section);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditContactPageSection(int id, string content, IFormFile? imageFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var section = await _context.HomePageSections.FindAsync(id);
            if (section == null || !section.SectionKey.StartsWith("contact-"))
                return Unauthorized();

            string? imagePath = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                if (section != null)
                {
                    var key = section.SectionKey ?? "contact";
                    var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "home", key);
                    Directory.CreateDirectory(uploadDir);
                    var fileName = $"{DateTime.UtcNow.Ticks}_{Path.GetFileName(imageFile.FileName)}";
                    var filePath = Path.Combine(uploadDir, fileName);
                    using (var stream = System.IO.File.Create(filePath))
                    {
                        await imageFile.CopyToAsync(stream);
                    }
                    imagePath = $"/uploads/home/{key}/{fileName}";
                }
            }
            await _homePageService.UpdateSectionAsync(id, content, user.Id, imagePath);
            TempData["SuccessMessage"] = "Section updated successfully!";
            if (!string.IsNullOrEmpty(imagePath)) TempData["ImagePath"] = imagePath;
            return RedirectToAction(nameof(EditContactPageSection), new { id });
        }

        // ===== VISUAL LIVE EDITOR API ENDPOINTS =====

        [HttpGet]
        public async Task<IActionResult> VisualEditor()
        {
            var sections = await _homePageService.GetAllSectionsAsync();
            return View(sections);
        }

        [HttpGet]
        public async Task<IActionResult> GetLivePagePreview(string page = "home")
        {
            var sections = await _homePageService.GetAllSectionsAsync();
            string html = "";

            switch (page.ToLower())
            {
                case "about":
                    html = await GenerateAboutPagePreview(sections);
                    break;
                case "services":
                    html = await GenerateServicesPagePreview(sections);
                    break;
                case "contact":
                    html = await GenerateContactPagePreview(sections);
                    break;
                default: // home
                    html = await GenerateHomePagePreview(sections);
                    break;
            }

            return Content(html, "text/html");
        }

        private async Task<string> GenerateHomePagePreview(List<DoableFinal.Models.HomePageSection> sections)
        {
            var heroTitle = sections.FirstOrDefault(s => s.SectionKey == "hero-title")?.Content ?? "QONNEC";
            var heroBody = sections.FirstOrDefault(s => s.SectionKey == "hero-body")?.Content ?? "Streamline your projects, collaborate with your team, and achieve better results.";
            var heroImagePath = sections.FirstOrDefault(s => s.SectionKey == "hero-image")?.ImagePath;
            var feature1Title = sections.FirstOrDefault(s => s.SectionKey == "feature-1-title")?.Content ?? "Task Management";
            var feature1Body = sections.FirstOrDefault(s => s.SectionKey == "feature-1-body")?.Content ?? "Organize and track tasks with our intuitive Kanban board system.";
            var feature2Title = sections.FirstOrDefault(s => s.SectionKey == "feature-2-title")?.Content ?? "Team Collaboration";
            var feature2Body = sections.FirstOrDefault(s => s.SectionKey == "feature-2-body")?.Content ?? "Work together seamlessly with real-time updates and communication tools.";
            var feature3Title = sections.FirstOrDefault(s => s.SectionKey == "feature-3-title")?.Content ?? "Progress Tracking";
            var feature3Body = sections.FirstOrDefault(s => s.SectionKey == "feature-3-body")?.Content ?? "Monitor project progress with detailed analytics and reporting.";
            var ctaTitle = sections.FirstOrDefault(s => s.SectionKey == "cta-title")?.Content ?? "Ready to Get Started?";
            var ctaBody = sections.FirstOrDefault(s => s.SectionKey == "cta-body")?.Content ?? "Join thousands of teams already using our platform to manage their projects.";

            var heroSection = sections.FirstOrDefault(s => s.SectionKey == "hero-title");
            var heroBodySection = sections.FirstOrDefault(s => s.SectionKey == "hero-body");
            var heroImageSection = sections.FirstOrDefault(s => s.SectionKey == "hero-image");
            var feature1TitleSection = sections.FirstOrDefault(s => s.SectionKey == "feature-1-title");
            var feature1BodySection = sections.FirstOrDefault(s => s.SectionKey == "feature-1-body");
            var feature2TitleSection = sections.FirstOrDefault(s => s.SectionKey == "feature-2-title");
            var feature2BodySection = sections.FirstOrDefault(s => s.SectionKey == "feature-2-body");
            var feature3TitleSection = sections.FirstOrDefault(s => s.SectionKey == "feature-3-title");
            var feature3BodySection = sections.FirstOrDefault(s => s.SectionKey == "feature-3-body");
            var ctaTitleSection = sections.FirstOrDefault(s => s.SectionKey == "cta-title");
            var ctaBodySection = sections.FirstOrDefault(s => s.SectionKey == "cta-body");

            var html = $@"
<style>
    .editable-section {{
        position: relative;
        transition: background-color 0.2s, box-shadow 0.2s;
        cursor: pointer;
    }}
    .editable-section:hover {{
        background-color: rgba(0, 123, 255, 0.05);
        box-shadow: inset 0 0 0 2px rgba(0, 123, 255, 0.3);
    }}
    .editable-section.active {{
        background-color: rgba(0, 123, 255, 0.1);
        box-shadow: inset 0 0 0 2px rgba(0, 123, 255, 0.6);
    }}
    .section-label {{
        position: absolute; top: 8px; left: 12px;
        background: rgba(0, 123, 255, 0.9);
        color: white; padding: 4px 10px; border-radius: 4px;
        font-size: 11px; font-weight: bold;
        z-index: 100; opacity: 0; transition: opacity 0.2s;
        pointer-events: none;
    }}
    .editable-section:hover .section-label,
    .editable-section.active .section-label {{ opacity: 1; }}
</style>
<section class=""hero-section bg-primary text-white py-5 d-flex align-items-center editable-section"" data-section-id=""{heroSection?.Id ?? 1}"" data-section-key=""hero-title"">
    <div class=""section-label"">Hero Title</div>
    <div class=""container""><div class=""row justify-content-center text-center""><div class=""col-lg-8""><h1 class=""display-4 fw-bold"">{System.Security.SecurityElement.Escape(heroTitle)}</h1></div></div></div>
</section>
<section class=""bg-primary text-white py-4 editable-section"" data-section-id=""{heroBodySection?.Id ?? 2}"" data-section-key=""hero-body"">
    <div class=""section-label"">Hero Description</div>
    <div class=""container""><div class=""row justify-content-center text-center""><div class=""col-lg-8""><p class=""lead mb-4"">{System.Security.SecurityElement.Escape(heroBody)}</p><div class=""d-flex gap-3 justify-content-center mt-4""><a href=""#"" class=""btn btn-light btn-lg"">Get Started</a><a href=""#features"" class=""btn btn-outline-light btn-lg"">Learn More</a></div></div></div></div>
</section>
{(!string.IsNullOrEmpty(heroImagePath) || heroImageSection != null ? $@"<section class=""bg-white py-5 editable-section"" data-section-id=""{heroImageSection?.Id ?? 11}"" data-section-key=""hero-image"">
    <div class=""section-label"">Hero Image (Click to Upload)</div>
    <div class=""container text-center"">
        {(!string.IsNullOrEmpty(heroImagePath) ? $@"<img src=""{heroImagePath}"" class=""img-fluid rounded"" alt=""Hero Image"" style=""max-width: 600px;"" />" : @"<div class=""alert alert-info"">No image uploaded yet. Click to upload one.</div>")}
    </div>
</section>" : "")}
<section id=""features"" class=""py-5"">
    <div class=""container""><h2 class=""text-center mb-5"">Why Choose Us</h2>
    <div class=""row g-4"">
        <div class=""col-md-4"">
            <div class=""card border-0 shadow-sm editable-section"" data-section-id=""{feature1TitleSection?.Id ?? 3}"" data-section-key=""feature-1-title"">
                <div class=""section-label"">Feature 1 Title (Click to Edit)</div>
                <div class=""card-body text-center p-2"">
                    <i class=""bi bi-kanban display-4 text-primary mb-2""></i>
                    <h3 class=""h6 fw-bold"">{System.Security.SecurityElement.Escape(feature1Title)}</h3>
                </div>
            </div>
            <div class=""card border-0 shadow-sm editable-section mt-2"" data-section-id=""{feature1BodySection?.Id ?? 8}"" data-section-key=""feature-1-body"">
                <div class=""section-label"">Description (Click to Edit)</div>
                <div class=""card-body text-center p-3"">
                    <p class=""text-muted small"">{System.Security.SecurityElement.Escape(feature1Body)}</p>
                </div>
            </div>
        </div>
        <div class=""col-md-4"">
            <div class=""card border-0 shadow-sm editable-section"" data-section-id=""{feature2TitleSection?.Id ?? 4}"" data-section-key=""feature-2-title"">
                <div class=""section-label"">Feature 2 Title (Click to Edit)</div>
                <div class=""card-body text-center p-2"">
                    <i class=""bi bi-people display-4 text-primary mb-2""></i>
                    <h3 class=""h6 fw-bold"">{System.Security.SecurityElement.Escape(feature2Title)}</h3>
                </div>
            </div>
            <div class=""card border-0 shadow-sm editable-section mt-2"" data-section-id=""{feature2BodySection?.Id ?? 9}"" data-section-key=""feature-2-body"">
                <div class=""section-label"">Description (Click to Edit)</div>
                <div class=""card-body text-center p-3"">
                    <p class=""text-muted small"">{System.Security.SecurityElement.Escape(feature2Body)}</p>
                </div>
            </div>
        </div>
        <div class=""col-md-4"">
            <div class=""card border-0 shadow-sm editable-section"" data-section-id=""{feature3TitleSection?.Id ?? 5}"" data-section-key=""feature-3-title"">
                <div class=""section-label"">Feature 3 Title (Click to Edit)</div>
                <div class=""card-body text-center p-2"">
                    <i class=""bi bi-graph-up display-4 text-primary mb-2""></i>
                    <h3 class=""h6 fw-bold"">{System.Security.SecurityElement.Escape(feature3Title)}</h3>
                </div>
            </div>
            <div class=""card border-0 shadow-sm editable-section mt-2"" data-section-id=""{feature3BodySection?.Id ?? 10}"" data-section-key=""feature-3-body"">
                <div class=""section-label"">Description (Click to Edit)</div>
                <div class=""card-body text-center p-3"">
                    <p class=""text-muted small"">{System.Security.SecurityElement.Escape(feature3Body)}</p>
                </div>
            </div>
        </div>
    </div></div>
</section>
<section class=""bg-light py-5 editable-section"" data-section-id=""{ctaTitleSection?.Id ?? 6}"" data-section-key=""cta-title"">
    <div class=""section-label"">CTA Title</div>
    <div class=""container text-center""><h2 class=""mb-4"">{System.Security.SecurityElement.Escape(ctaTitle)}</h2></div>
</section>
<section class=""bg-light py-4 editable-section"" data-section-id=""{ctaBodySection?.Id ?? 7}"" data-section-key=""cta-body"">
    <div class=""section-label"">CTA Description</div>
    <div class=""container text-center""><p class=""lead mb-4"">{System.Security.SecurityElement.Escape(ctaBody)}</p><a href=""#"" class=""btn btn-primary btn-lg"">Create Free Account</a></div>
</section>";
            return html;
        }

        private async Task<string> GenerateAboutPagePreview(List<DoableFinal.Models.HomePageSection> sections)
        {
            var sections_about = sections.Where(s => s.SectionKey.StartsWith("about-")).OrderBy(s => s.SectionOrder).ToList();
            
            var html = @"<style>
    .editable-section { position: relative; transition: background-color 0.2s, box-shadow 0.2s; cursor: pointer; }
    .editable-section:hover { background-color: rgba(0, 123, 255, 0.05); box-shadow: inset 0 0 0 2px rgba(0, 123, 255, 0.3); }
    .editable-section.active { background-color: rgba(0, 123, 255, 0.1); box-shadow: inset 0 0 0 2px rgba(0, 123, 255, 0.6); }
    .section-label { position: absolute; top: 8px; left: 12px; background: rgba(0, 123, 255, 0.9); color: white; padding: 4px 10px; border-radius: 4px; font-size: 11px; font-weight: bold; z-index: 100; opacity: 0; transition: opacity 0.2s; pointer-events: none; }
    .editable-section:hover .section-label, .editable-section.active .section-label { opacity: 1; }
</style>
<section class=""py-5 bg-light"">
    <div class=""container""><h1 class=""display-5 fw-bold mb-4"">About Us</h1></div>
</section>";

            int index = 1;
            foreach (var section in sections_about)
            {
                var bgClass = index % 2 == 0 ? "bg-white" : "bg-light";
                var sectionHtml = $@"<section class=""py-5 {bgClass} editable-section"" data-section-id=""{section.Id}"" data-section-key=""{section.SectionKey}"">
    <div class=""section-label"">{section.DisplayName} (Click to Edit)</div>
    <div class=""container"">
        {(!string.IsNullOrEmpty(section.ImagePath) ? $@"<div class=""mb-4""><img src=""{section.ImagePath}"" class=""img-fluid rounded"" alt=""{section.DisplayName}"" style=""max-width: 300px;"" /></div>" : "")}
        <div class=""lead"">{System.Security.SecurityElement.Escape(section.Content)}</div>
    </div>
</section>";
                html += sectionHtml;
                index++;
            }

            return html;
        }

        private async Task<string> GenerateServicesPagePreview(List<DoableFinal.Models.HomePageSection> sections)
        {
            var sections_services = sections.Where(s => s.SectionKey.StartsWith("services-")).OrderBy(s => s.SectionOrder).ToList();
            
            var html = @"<style>
    .editable-section { position: relative; transition: background-color 0.2s, box-shadow 0.2s; cursor: pointer; }
    .editable-section:hover { background-color: rgba(0, 123, 255, 0.05); box-shadow: inset 0 0 0 2px rgba(0, 123, 255, 0.3); }
    .editable-section.active { background-color: rgba(0, 123, 255, 0.1); box-shadow: inset 0 0 0 2px rgba(0, 123, 255, 0.6); }
    .section-label { position: absolute; top: 8px; left: 12px; background: rgba(0, 123, 255, 0.9); color: white; padding: 4px 10px; border-radius: 4px; font-size: 11px; font-weight: bold; z-index: 100; opacity: 0; transition: opacity 0.2s; pointer-events: none; }
    .editable-section:hover .section-label, .editable-section.active .section-label { opacity: 1; }
</style>
<section class=""py-5 bg-light"">
    <div class=""container""><h1 class=""display-5 fw-bold mb-4"">Our Services</h1></div>
</section>";

            int index = 1;
            foreach (var section in sections_services)
            {
                var bgClass = index % 2 == 0 ? "bg-white" : "bg-light";
                var sectionHtml = $@"<section class=""py-5 {bgClass} editable-section"" data-section-id=""{section.Id}"" data-section-key=""{section.SectionKey}"">
    <div class=""section-label"">{section.DisplayName} (Click to Edit)</div>
    <div class=""container"">
        {(!string.IsNullOrEmpty(section.ImagePath) ? $@"<div class=""mb-4""><img src=""{section.ImagePath}"" class=""img-fluid rounded"" alt=""{section.DisplayName}"" style=""max-width: 300px;"" /></div>" : "")}
        <div class=""lead"">{System.Security.SecurityElement.Escape(section.Content)}</div>
    </div>
</section>";
                html += sectionHtml;
                index++;
            }

            return html;
        }

        private async Task<string> GenerateContactPagePreview(List<DoableFinal.Models.HomePageSection> sections)
        {
            var sections_contact = sections.Where(s => s.SectionKey.StartsWith("contact-")).OrderBy(s => s.SectionOrder).ToList();
            
            var html = @"<style>
    .editable-section { position: relative; transition: background-color 0.2s, box-shadow 0.2s; cursor: pointer; }
    .editable-section:hover { background-color: rgba(0, 123, 255, 0.05); box-shadow: inset 0 0 0 2px rgba(0, 123, 255, 0.3); }
    .editable-section.active { background-color: rgba(0, 123, 255, 0.1); box-shadow: inset 0 0 0 2px rgba(0, 123, 255, 0.6); }
    .section-label { position: absolute; top: 8px; left: 12px; background: rgba(0, 123, 255, 0.9); color: white; padding: 4px 10px; border-radius: 4px; font-size: 11px; font-weight: bold; z-index: 100; opacity: 0; transition: opacity 0.2s; pointer-events: none; }
    .editable-section:hover .section-label, .editable-section.active .section-label { opacity: 1; }
</style>
<section class=""py-5 bg-light"">
    <div class=""container""><h1 class=""display-5 fw-bold mb-4"">Contact Us</h1></div>
</section>";

            int index = 1;
            foreach (var section in sections_contact)
            {
                var bgClass = index % 2 == 0 ? "bg-white" : "bg-light";
                var sectionHtml = $@"<section class=""py-5 {bgClass} editable-section"" data-section-id=""{section.Id}"" data-section-key=""{section.SectionKey}"">
    <div class=""section-label"">{section.DisplayName} (Click to Edit)</div>
    <div class=""container"">
        {(!string.IsNullOrEmpty(section.ImagePath) ? $@"<div class=""mb-4""><img src=""{section.ImagePath}"" class=""img-fluid rounded"" alt=""{section.DisplayName}"" style=""max-width: 300px;"" /></div>" : "")}
        <div class=""lead"">{System.Security.SecurityElement.Escape(section.Content)}</div>
    </div>
</section>";
                html += sectionHtml;
                index++;
            }

            return html;
        }

        [HttpGet]
        public async Task<IActionResult> GetSectionData(int id)
        {
            try
            {
                var section = await _context.HomePageSections.FindAsync(id);
                if (section == null)
                    return Json(new { success = false, message = "Section not found" });

                // Check if section key contains "image" to determine if it supports image upload
                var supportsImage = (section.SectionKey ?? "").Contains("image", StringComparison.OrdinalIgnoreCase);

                return Json(new
                {
                    success = true,
                    sectionKey = section.SectionKey,
                    displayName = section.DisplayName,
                    content = section.Content,
                    imagePath = section.ImagePath,
                    supportsImage = supportsImage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting section data");
                return Json(new { success = false, message = "Error loading section" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSectionData(int id, string? content, IFormFile? imageFile)
        {
            try
            {
                _logger.LogInformation($"SaveSectionData called: id={id}, content length={content?.Length ?? 0}, has image={imageFile != null}");
                
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found in SaveSectionData");
                    return Json(new { success = false, message = "User not found" });
                }

                var section = await _context.HomePageSections.FindAsync(id);
                if (section == null)
                {
                    _logger.LogWarning($"Section not found: id={id}");
                    return Json(new { success = false, message = "Section not found" });
                }

                // For image-only sections, allow empty content
                bool isImageOnly = section.SectionKey != null && section.SectionKey.Contains("image", StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(content) && !isImageOnly && imageFile == null)
                {
                    _logger.LogWarning("Content is empty in SaveSectionData");
                    return Json(new { success = false, message = "Content cannot be empty" });
                }

                string? imagePath = null;
                if (imageFile != null && imageFile.Length > 0)
                {
                    try
                    {
                        var key = section.SectionKey ?? "home";
                        var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "home", key);
                        Directory.CreateDirectory(uploadDir);
                        var fileName = $"{DateTime.UtcNow.Ticks}_{Path.GetFileName(imageFile.FileName)}";
                        var filePath = Path.Combine(uploadDir, fileName);
                        using (var stream = System.IO.File.Create(filePath))
                        {
                            await imageFile.CopyToAsync(stream);
                        }
                        imagePath = $"/uploads/home/{key}/{fileName}";
                        _logger.LogInformation($"Image uploaded: {imagePath}");
                    }
                    catch (Exception imgEx)
                    {
                        _logger.LogError(imgEx, "Error uploading image");
                        return Json(new { success = false, message = "Error uploading image: " + imgEx.Message });
                    }
                }

                await _homePageService.UpdateSectionAsync(id, content, user.Id, imagePath);
                _logger.LogInformation($"Section updated successfully: id={id}");
                
                var response = new { success = true, message = "Section updated successfully" };
                return Json(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving section data");
                return Json(new { success = false, message = "Error saving section: " + ex.Message });
            }
        }
    }
}