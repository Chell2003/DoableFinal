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
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, TimelineAdjustmentService timelineAdjustmentService, NotificationService notificationService, ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _timelineAdjustmentService = timelineAdjustmentService;
            _notificationService = notificationService;
            _logger = logger;
        }

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

            TempData["SuccessMessage"] = "Inquiry marked as handled.";
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

            if (!ModelState.IsValid)
            {
                foreach (var modelStateVal in ModelState.Values)
                {
                    if (modelStateVal.Errors.Count > 0)
                    {
                        foreach (var error in modelStateVal.Errors)
                        {
                            _logger.LogError($"Validation error: {error.ErrorMessage}");
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

            try
            {
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
                if (model.Role == "Employee" || model.Role == "Project Manager")
                {
                    user.ResidentialAddress = model.ResidentialAddress;
                    user.MobileNumber = model.MobileNumber;
                    user.Birthday = model.Birthday;
                    user.TinNumber = model.TinNumber;
                    user.PagIbigAccount = model.PagIbigAccount;
                    user.Position = model.Position;
                }
                else if (model.Role == "Client")
                {
                    user.CompanyName = model.CompanyName;
                    user.CompanyAddress = model.CompanyAddress;
                    user.CompanyType = model.CompanyType;
                    user.Designation = model.Designation;
                    user.MobileNumber = model.MobileNumber;
                    user.TinNumber = model.TinNumber;
                }

                // Check for existing email
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                    ViewBag.Role = model.Role;
                    return View(model);
                }

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    try
                    {
                        await _userManager.AddToRoleAsync(user, model.Role);
                        TempData["SuccessMessage"] = $"{model.Role} account created successfully.";
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
            var projectManagers = (await _userManager.GetUsersInRoleAsync("Project Manager"))
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

                TempData["SuccessMessage"] = "Project created successfully.";
                return RedirectToAction(nameof(Projects));
            }

            return await PrepareCreateProjectViewModel(model);
        }

        private async Task<IActionResult> PrepareCreateProjectViewModel(CreateProjectViewModel model)
        {
            var clients = (await _userManager.GetUsersInRoleAsync("Client"))
                .Where(u => !u.IsArchived)
                .ToList();
            var projectManagers = (await _userManager.GetUsersInRoleAsync("Project Manager"))
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
                EmailNotificationsEnabled = user.EmailNotificationsEnabled
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

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.EmailNotificationsEnabled = model.EmailNotificationsEnabled;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Profile updated successfully.";
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

            TempData["SuccessMessage"] = $"Two-factor authentication has been {(!currentStatus ? "enabled" : "disabled")}.";
            return RedirectToAction(nameof(Profile));
        }

        // User Management
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
                if (!currentRoles.Contains(model.Role))
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, model.Role);
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
                    TempData["SuccessMessage"] = "User updated successfully.";
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
                TempData["SuccessMessage"] = "User deleted successfully.";
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

                TempData["SuccessMessage"] = "Project updated successfully.";
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

            TempData["SuccessMessage"] = "Project has been archived successfully.";
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

            TempData["SuccessMessage"] = "Project has been unarchived successfully.";
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

            TempData["SuccessMessage"] = "Task has been unarchived successfully.";
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

            // Update project status
            var oldStatus = project.Status;
            project.Status = "Completed";
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify relevant parties
            await _notificationService.NotifyProjectUpdateAsync(project, $"Project '{project.Name}' has been marked as completed");

            TempData["SuccessMessage"] = "Project has been marked as completed.";
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

                TempData["SuccessMessage"] = "Task updated successfully.";
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

            TempData["SuccessMessage"] = "Task has been archived successfully.";
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

            TempData["SuccessMessage"] = "Comment posted successfully.";
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
            TempData["SuccessMessage"] = "Task has been confirmed as completed.";
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

                TempData["SuccessMessage"] = "Task created successfully.";
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
                .Where(t => !t.Project.IsArchived)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(t => t.Status == statusFilter);
            }

            var tickets = await query
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
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

            TempData["SuccessMessage"] = "Ticket status updated successfully.";
            return RedirectToAction(nameof(Tickets));
        }
    }
}