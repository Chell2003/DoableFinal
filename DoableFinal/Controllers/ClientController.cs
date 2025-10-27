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

        public async Task<IActionResult> Projects()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            var projects = await _context.Projects
                .Include(p => p.ProjectManager)
                .Where(p => p.ClientId == currentUser.Id && !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.ProjectProgress = await GetProjectProgress(projects);

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

            // Get project team
            ViewBag.ProjectTeam = await _context.ProjectTeams
                .Include(pt => pt.User)
                .Where(pt => pt.ProjectId == id)
                .Select(pt => pt.User)
                .ToListAsync();

            // Get member task counts
            ViewBag.MemberTaskCounts = await GetMemberTaskCounts(ViewBag.ProjectTeam);

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
                EmailNotificationsEnabled = user.EmailNotificationsEnabled
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
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Your password has been changed successfully.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
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

            TempData["SuccessMessage"] = "Comment posted successfully.";
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
        public async Task<IActionResult> MarkNotificationAsRead(int id)
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

        public async Task<IActionResult> Tickets()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            var tickets = await _context.Tickets
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo)
                .Where(t => t.CreatedById == currentUser.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

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
            debug.AppendLine($"Received Ticket POST - Title: {model?.Title}, ProjectId: {model?.ProjectId}");

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

            var project = await _context.Projects
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
                debug.AppendLine($"SaveChanges rows: {rows}");
                TempData["Debug"] = debug.ToString();

                if (project.ProjectManagerId != null)
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
                    await _notificationService.CreateNotification(
                        admin.Id,
                        "New Support Ticket",
                        $"New ticket created by {user.FirstName} {user.LastName} for project {project.Name}",
                        $"/Ticket/Details/{newTicket.Id}"
                    );
                }

                TempData["TicketMessage"] = "Support ticket created successfully.";
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
                .FirstOrDefaultAsync(t => t.Id == id && t.Project.ClientId == currentUser.Id);

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
    }
}