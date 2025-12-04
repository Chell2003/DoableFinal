using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using DoableFinal.Services;
using System.Security.Claims;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DoableFinal.Controllers
{
    [Authorize(Roles = "Client")]
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;

        public ClientController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            // Get statistics
            ViewBag.ProjectCount = await _context.Projects
                .Where(p => p.ClientId == currentUser.Id && !p.IsArchived)
                .CountAsync();

            ViewBag.TotalTasks = await _context.Tasks
                .Include(t => t.Project)
                .Where(t => t.Project.ClientId == currentUser.Id && 
                       !t.IsArchived && !t.Project.IsArchived)
                .CountAsync();

            ViewBag.CompletedTasks = await _context.Tasks
                .Include(t => t.Project)
                .Where(t => t.Project.ClientId == currentUser.Id && 
                       t.Status == "Completed" && 
                       !t.IsArchived && !t.Project.IsArchived)
                .CountAsync();

            ViewBag.OverdueTasks = await _context.Tasks
                .Include(t => t.Project)
                .Where(t => t.Project.ClientId == currentUser.Id &&
                           t.DueDate < DateTime.UtcNow &&
                           t.Status != "Completed" &&
                           !t.IsArchived && !t.Project.IsArchived)
                .CountAsync();

            // Get recent projects
            ViewBag.MyProjects = await _context.Projects
                .Include(p => p.ProjectManager)
                .Where(p => p.ClientId == currentUser.Id && !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Calculate project progress
            ViewBag.ProjectProgress = await GetProjectProgress(ViewBag.MyProjects);

            // Get project team members
            ViewBag.ProjectTeam = await _context.ProjectTeams
                .Include(pt => pt.User)
                .Include(pt => pt.Project)
                .Where(pt => pt.Project.ClientId == currentUser.Id && !pt.Project.IsArchived)
                .Select(pt => pt.User)
                .Distinct()
                .Take(5)
                .ToListAsync();

            // Get member task counts
            ViewBag.MemberTaskCounts = await GetMemberTaskCounts(ViewBag.ProjectTeam);

            // Get recent tasks with their assignments
            ViewBag.ProjectTasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Where(t => t.Project.ClientId == currentUser.Id && 
                       !t.IsArchived && !t.Project.IsArchived)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View();
        }

        public async Task<IActionResult> Projects(string? q = "", string? statusFilter = "", string? fromDate = "", string? toDate = "")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            var query = _context.Projects
                .Include(p => p.ProjectManager)
                .Where(p => p.ClientId == currentUser.Id && !p.IsArchived)
                .AsQueryable();

            // Search by project name
            if (!string.IsNullOrEmpty(q))
            {
                var searchTerm = q.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(searchTerm));
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(p => p.Status == statusFilter);
            }

            // Date range filtering
            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var startDate))
            {
                query = query.Where(p => p.CreatedAt.Date >= startDate.Date);
            }

            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var endDate))
            {
                query = query.Where(p => p.CreatedAt.Date <= endDate.Date);
            }

            var projects = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.ProjectProgress = await GetProjectProgress(projects);
            ViewBag.SearchQuery = q;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.AvailableStatuses = new List<string> { "Not Started", "In Progress", "On Hold", "Completed" };

            return View(projects);
        }

        public async Task<IActionResult> ProjectDetails(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            var project = await _context.Projects
                .Include(p => p.ProjectManager)
                .FirstOrDefaultAsync(p => p.Id == id && p.ClientId == currentUser.Id);

            if (project == null)
            {
                return NotFound();
            }

            // Calculate project progress
            ViewBag.ProjectProgress = (await GetProjectProgress(new[] { project }))[project.Id];

            // Get project team including the project manager
            var projectTeam = new List<ApplicationUser>();
            
            // Add project manager first
            if (project.ProjectManager != null)
            {
                projectTeam.Add(project.ProjectManager);
            }

            // Add team members from ProjectTeams
            var teamMembers = await _context.ProjectTeams
                .Include(pt => pt.User)
                .Where(pt => pt.ProjectId == id)
                .Select(pt => pt.User)
                .ToListAsync();

            projectTeam.AddRange(teamMembers);

            ViewBag.ProjectTeam = projectTeam;

            // Get member task counts
            ViewBag.MemberTaskCounts = await GetMemberTaskCounts(projectTeam);

            // Get recent tasks with their assignments
            ViewBag.RecentTasks = await _context.Tasks
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Where(t => t.ProjectId == id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View(project);
        }

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

                // Client-specific values
                CompanyName = user.CompanyName ?? string.Empty,
                CompanyAddress = user.CompanyAddress ?? string.Empty,
                CompanyType = user.CompanyType ?? string.Empty,
                Designation = user.Designation ?? string.Empty,
                MobileNumber = user.MobileNumber ?? string.Empty,
                TinNumber = user.TinNumber ?? string.Empty,

                // Common additional fields (may be empty for clients)
                ResidentialAddress = user.ResidentialAddress ?? string.Empty,
                Birthday = user.Birthday,
                PagIbigAccount = user.PagIbigAccount ?? string.Empty,
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
            // Defensive: some earlier runs caused model binder to add spurious
            // validation errors for the select-list properties (Projects, Assignees,
            // PriorityLevels, TicketTypes) even though they are only used to render
            // the form. Remove any such errors so user-entered fields control validity.
            ModelState.Remove("Projects");
            ModelState.Remove("Assignees");
            ModelState.Remove("PriorityLevels");
            ModelState.Remove("TicketTypes");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var phoneAttr = new DoableFinal.Validation.PhonePHAttribute();
            var tinAttr = new DoableFinal.Validation.TinAttribute();
            var pagIbigAttr = new DoableFinal.Validation.PagIbigAttribute();

            if (!string.IsNullOrWhiteSpace(model.MobileNumber) && !phoneAttr.IsValid(model.MobileNumber))
            {
                ModelState.AddModelError("MobileNumber", "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits). ");
                return View(model);
            }
            if (!string.IsNullOrWhiteSpace(model.TinNumber) && !tinAttr.IsValid(model.TinNumber))
            {
                ModelState.AddModelError("TinNumber", "Invalid TIN. Expected: XXX-XXX-XXX or XXX-XXX-XXX-XXX.");
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.EmailNotificationsEnabled = model.EmailNotificationsEnabled;
            // Save client-specific fields
            user.CompanyName = model.CompanyName;
            user.CompanyAddress = model.CompanyAddress;
            user.CompanyType = model.CompanyType;
            user.Designation = model.Designation;
            user.MobileNumber = model.MobileNumber;
            user.TinNumber = model.TinNumber;

            // Save common/additional fields if provided
            user.ResidentialAddress = model.ResidentialAddress;
            user.Position = model.Position;

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

            return View(model);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(s => !string.IsNullOrWhiteSpace(s));
                TempData["PasswordErrorMessage"] = string.Join(" ", errors);
                return RedirectToAction(nameof(Profile));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                TempData["PasswordSuccessMessage"] = "Your password has been changed successfully.";
                return RedirectToAction(nameof(Profile));
            }

            var identityErrors = result.Errors.Select(e => e.Description).Where(s => !string.IsNullOrWhiteSpace(s));
            TempData["PasswordErrorMessage"] = identityErrors.Any()
                ? string.Join(" ", identityErrors)
                : "Current password is incorrect or new password does not meet requirements.";

            return RedirectToAction(nameof(Profile));
        }

        public async Task<IActionResult> Tasks(int? projectId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Include(t => t.CreatedBy)
                .Where(t => t.Project.ClientId == userId && 
                       !t.IsArchived && !t.Project.IsArchived);

            if (projectId.HasValue)
            {
                // If projectId is provided, filter tasks for that specific project
                query = query.Where(t => t.ProjectId == projectId);

                // Get the project details to show in the view
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.ClientId == userId);

                if (project == null)
                {
                    return NotFound();
                }

                ViewBag.ProjectName = project.Name;
                ViewBag.ProjectId = project.Id;
            }


            // Order: High priority first, then Medium/others, then Low last, then by CreatedAt
            var tasks = await query
                .OrderBy(t => t.Priority == "Low" ? 2 : t.Priority == "High" ? 0 : 1)
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tasks);
        }

        [Authorize(Roles = "Client")]
        public async Task<IActionResult> TaskDetails(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var task = await _context.Tasks
                .Include(t => t.Project)
                    .ThenInclude(p => p.ProjectTeams)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Include(t => t.Comments)
                    .ThenInclude(c => c.CreatedBy)
                .FirstOrDefaultAsync(t => t.Id == id && t.Project.ClientId == userId);

            if (task == null || task.Project == null)
            {
                return NotFound();
            }

            // Initialize Comments collection if null
            if (task.Comments == null)
            {
                task.Comments = new List<TaskComment>();
            }
            else
            {
                // Order comments by creation date
                task.Comments = task.Comments.OrderByDescending(c => c.CreatedAt).ToList();
            }

            // Load project manager details to ensure it's available for the view
            await _context.Entry(task.Project)
                .Reference(p => p.ProjectManager)
                .LoadAsync();

            // Load client details
            await _context.Entry(task.Project)
                .Reference(p => p.Client)
                .LoadAsync();

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
            if (currentUser == null)
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

            TempData["TicketMessage"] = "Comment posted successfully.";
            return RedirectToAction("TaskDetails", new { id = taskId });
        }

        public async Task<IActionResult> Notifications()
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier))
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }

        [HttpPost]
        public async Task<IActionResult> MarkNotificationAsRead(int id, string? returnUrl = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification == null)
            {
                return NotFound();
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            // If returnUrl is provided, redirect to it; otherwise go back to notifications
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Notifications));
        }

        private async Task<Dictionary<int, int>> GetProjectProgress(IEnumerable<Project> projects)
        {
            var progress = new Dictionary<int, int>();
            foreach (var project in projects)
            {
                var totalTasks = await _context.Tasks
                    .Where(t => t.ProjectId == project.Id)
                    .CountAsync();

                var completedTasks = await _context.Tasks
                    .Where(t => t.ProjectId == project.Id && t.Status == "Completed")
                    .CountAsync();

                progress[project.Id] = totalTasks > 0
                    ? (int)Math.Round((double)completedTasks / totalTasks * 100)
                    : 0;
            }
            return progress;
        }

        private async Task<Dictionary<string, int>> GetMemberTaskCounts(IEnumerable<ApplicationUser> members)
        {
            var counts = new Dictionary<string, int>();
            foreach (var member in members)
            {
                var taskCount = await _context.TaskAssignments
                    .Where(ta => ta.EmployeeId == member.Id)
                    .CountAsync();
                counts[member.Id] = taskCount;
            }
            return counts;
        }

        public async Task<IActionResult> Tickets(string? q = "", string? statusFilter = "", string? fromDate = "", string? toDate = "")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            var query = _context.Tickets
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo)
                .Where(t => t.CreatedById == currentUser.Id)
                .AsQueryable();

            // Search by title
            if (!string.IsNullOrEmpty(q))
            {
                var searchTerm = q.ToLower();
                query = query.Where(t => t.Title.ToLower().Contains(searchTerm));
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(t => t.Status == statusFilter);
            }

            // Date range filtering
            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var startDate))
            {
                query = query.Where(t => t.CreatedAt.Date >= startDate.Date);
            }

            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var endDate))
            {
                query = query.Where(t => t.CreatedAt.Date <= endDate.Date);
            }

            var tickets = await query
                .OrderByDescending(t => t.Priority == "Critical" ? 4 : t.Priority == "High" ? 3 : t.Priority == "Medium" ? 2 : t.Priority == "Low" ? 1 : 0)
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.SearchQuery = q;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.AvailableStatuses = new List<string> { "Open", "In Progress", "Resolved", "Closed" };
            return View(tickets);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTicket()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            var projects = await _context.Projects
                .Where(p => p.ClientId == currentUser.Id && !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                })
                .ToListAsync();

            var vm = new DoableFinal.ViewModels.CreateTicketViewModel
            {
                Projects = projects,
                PriorityLevels = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Low", Text = "Low" },
                    new SelectListItem { Value = "Medium", Text = "Medium" },
                    new SelectListItem { Value = "High", Text = "High" },
                    new SelectListItem { Value = "Critical", Text = "Critical" }
                },
                TicketTypes = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Bug", Text = "Bug" },
                    new SelectListItem { Value = "Feature Request", Text = "Feature Request" },
                    new SelectListItem { Value = "Support", Text = "Support" },
                    new SelectListItem { Value = "Other", Text = "Other" }
                }
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTicket(DoableFinal.ViewModels.CreateTicketViewModel model)
        {
            var debug = new System.Text.StringBuilder();
            

            if (model == null)
            {
                TempData["Error"] = "Invalid ticket data.";
                return RedirectToAction(nameof(CreateTicket));
            }

            if (!ModelState.IsValid)
            {
                debug.AppendLine("ModelState invalid:");
                foreach (var err in ModelState.Values.SelectMany(v => v.Errors))
                {
                    debug.AppendLine(err.ErrorMessage);
                }

                // Reload projects
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    model.Projects = await _context.Projects
                        .Where(p => p.ClientId == currentUser.Id && !p.IsArchived)
                        .OrderByDescending(p => p.CreatedAt)
                        .Select(p => new SelectListItem
                        {
                            Value = p.Id.ToString(),
                            Text = p.Name
                        })
                        .ToListAsync();
                }

                TempData["Debug"] = debug.ToString();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            Project project = null;
            if (model.ProjectId.HasValue)
            {
                project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == model.ProjectId && p.ClientId == user.Id);
                if (project == null)
                {
                    ModelState.AddModelError("ProjectId", "Invalid project selection");
                    model.Projects = await _context.Projects
                        .Where(p => p.ClientId == user.Id && !p.IsArchived)
                        .OrderByDescending(p => p.CreatedAt)
                        .Select(p => new SelectListItem
                        {
                            Value = p.Id.ToString(),
                            Text = p.Name
                        })
                        .ToListAsync();
                    TempData["Debug"] = "Project selection invalid";
                    return View(model);
                }
            }

            var newTicket = new Ticket
            {
                Title = model.Title,
                Description = model.Description,
                Priority = string.IsNullOrEmpty(model.Priority) ? "Medium" : model.Priority,
                Status = "Open",
                Type = model.Type,
                ProjectId = model.ProjectId,
                CreatedById = user.Id,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _context.Tickets.Add(newTicket);
                var rows = await _context.SaveChangesAsync();
                debug.AppendLine($"Your ticket '{newTicket.Title}' has been created successfully.");
                // TempData["Debug"] = debug.ToString();
                // Also set a ticket-specific success message so the shared alerts partial
                // renders this as a success (green) alert instead of the debug (yellow) alert.
                TempData["TicketMessage"] = $"Your ticket '{newTicket.Title}' has been created successfully.";

                // Notify project manager if ticket is associated with a project
                if (project?.ProjectManagerId != null)
                {
                    await _notificationService.CreateNotification(
                        project.ProjectManagerId,
                        "New Support Ticket",
                        $"New ticket created by {user.FirstName} {user.LastName} for project {project.Name}",
                        $"/Ticket/Details/{newTicket.Id}"
                    );
                }

                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                foreach (var admin in adminUsers)
                {
                    var message = project != null
                        ? $"New ticket created by {user.FirstName} {user.LastName} for project {project.Name}"
                        : $"New ticket created by {user.FirstName} {user.LastName}";

                    await _notificationService.CreateNotification(
                        admin.Id,
                        "New Support Ticket",
                        message,
                        $"/Ticket/Details/{newTicket.Id}"
                    );
                }

                // Send notification to the client about successful ticket creation
                await _notificationService.CreateNotification(
                    user.Id,
                    "Ticket Created",
                    $"Your ticket '{newTicket.Title}' has been created successfully.",
                    $"/Client/TicketDetails/{newTicket.Id}"
                );
                return RedirectToAction(nameof(Tickets));
            }
            catch (Exception ex)
            {
                debug.AppendLine("Exception when saving ticket: " + ex.Message);
                TempData["Error"] = "Failed to create ticket: " + ex.Message;
                TempData["Debug"] = debug.ToString();

                model.Projects = await _context.Projects
                    .Where(p => p.ClientId == user.Id && !p.IsArchived)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new SelectListItem
                    {
                        Value = p.Id.ToString(),
                        Text = p.Name
                    })
                    .ToListAsync();
                return View(model);
            }
        }

        public async Task<IActionResult> TicketDetails(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo)
                .Include(t => t.Project)
                .Include(t => t.Comments.OrderByDescending(c => c.CreatedAt))
                    .ThenInclude(c => c.CreatedBy)
                .Include(t => t.Attachments.OrderByDescending(a => a.UploadedAt))
                    .ThenInclude(a => a.UploadedBy)
                .FirstOrDefaultAsync(t => t.Id == id && 
                    (t.CreatedById == currentUser.Id || (t.Project != null && t.Project.ClientId == currentUser.Id)));

            if (ticket == null)
            {
                return NotFound();
            }

            var viewModel = new DoableFinal.ViewModels.TicketDetailsViewModel
            {
                Ticket = ticket,
                Comments = ticket.Comments?.ToList() ?? new List<DoableFinal.Models.TicketComment>(),
                Attachments = ticket.Attachments?.ToList() ?? new List<DoableFinal.Models.TicketAttachment>()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTicketComment(int ticketId, string comment)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets
                .FirstOrDefaultAsync(t => t.Id == ticketId && t.CreatedById == currentUser.Id);

            if (ticket == null)
            {
                return NotFound();
            }

            var ticketComment = new TicketComment
            {
                TicketId = ticketId,
                CommentText = comment,
                CreatedById = currentUser.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.TicketComments.Add(ticketComment);
            await _context.SaveChangesAsync();

            TempData["TicketMessage"] = "Comment added successfully.";
            return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTicketComment(int commentId, string comment)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            var ticketComment = await _context.TicketComments
                .Include(tc => tc.Ticket)
                .FirstOrDefaultAsync(tc => tc.Id == commentId && tc.CreatedById == currentUser.Id);

            if (ticketComment == null || ticketComment.IsArchived)
            {
                return NotFound();
            }

            ticketComment.CommentText = comment;
            ticketComment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["TicketMessage"] = "Comment updated successfully.";
            return RedirectToAction(nameof(TicketDetails), new { id = ticketComment.TicketId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveTicketComment(int commentId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            var ticketComment = await _context.TicketComments
                .Include(tc => tc.Ticket)
                .FirstOrDefaultAsync(tc => tc.Id == commentId && tc.CreatedById == currentUser.Id);

            if (ticketComment == null)
            {
                return NotFound();
            }

            // Soft delete
            ticketComment.IsArchived = true;
            ticketComment.ArchivedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["TicketMessage"] = "Comment removed successfully.";
            return RedirectToAction(nameof(TicketDetails), new { id = ticketComment.TicketId });
        }
    }
}