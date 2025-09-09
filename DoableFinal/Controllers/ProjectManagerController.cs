
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using DoableFinal.Data;
using DoableFinal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DoableFinal.Controllers
{
    [Authorize(Roles = "Project Manager")]
    public class ProjectManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TimelineAdjustmentService _timelineAdjustmentService;
        private readonly ILogger<ProjectManagerController> _logger;
        private readonly NotificationService _notificationService;

        public ProjectManagerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, TimelineAdjustmentService timelineAdjustmentService, ILogger<ProjectManagerController> logger, NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _timelineAdjustmentService = timelineAdjustmentService;
            _logger = logger;
            _notificationService = notificationService;
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
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            var userId = currentUser.Id;

            // Get statistics
            ViewBag.MyProjects = await _context.Projects
                .Where(p => p.ProjectManagerId != null && 
                           p.ProjectManagerId == userId && 
                           !p.IsArchived)
                .CountAsync();

            ViewBag.MyTasks = await _context.Tasks
                .Where(t => t.Project != null && t.Project.ProjectManagerId != null && t.Project.ProjectManagerId == userId)
                .CountAsync();

            ViewBag.OverdueTasks = await _context.Tasks
                .Where(t => t.Project != null && t.Project.ProjectManagerId != null && t.Project.ProjectManagerId == userId &&
                            t.DueDate < DateTime.UtcNow &&
                            t.Status != "Completed")
                .CountAsync();

            // Get recent projects
            ViewBag.RecentProjects = await _context.Projects
                .Where(p => p.ProjectManagerId != null && p.ProjectManagerId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Get team members
            ViewBag.TeamMembers = await _context.ProjectTeams
                .Include(pt => pt.User)
                .Where(pt => pt.Project != null && pt.Project.ProjectManagerId != null && pt.Project.ProjectManagerId == userId)
                .Select(pt => pt.User)
                .Distinct()
                .ToListAsync();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Tasks()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            // Fetch all tasks for projects managed by the current Project Manager
            var tasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Where(t => t.Project != null && 
                           t.Project.ProjectManagerId != null && 
                           t.Project.ProjectManagerId == currentUser.Id && 
                           !t.IsArchived)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tasks);
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

            // Only allow the assigned project manager
            var currentUser = await _userManager.GetUserAsync(User);
            if (project.ProjectManagerId != currentUser?.Id)
            {
                return Forbid();
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
            project.Status = "Completed";
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Notify relevant parties (if you have a notification service)
            if (_notificationService != null)
            {
                await _notificationService.NotifyProjectUpdateAsync(project, $"Project '{project.Name}' has been marked as completed");
            }

            TempData["SuccessMessage"] = "Project has been marked as completed.";
            return RedirectToAction(nameof(ProjectDetails), new { id });
        }

        [HttpGet]
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
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null || task.Project == null || task.Project.ProjectTeams == null)
            {
                return NotFound();
            }

            // Initialize Comments collection if null
            if (task.Comments == null)
            {
                task.Comments = new List<TaskComment>();
            }

            // Check if the user is part of the project team or assigned to the task
            var isUserInProject = task.Project.ProjectTeams.Any(pt => pt.UserId == userId);
            var isUserAssignedToTask = task.TaskAssignments.Any(ta => ta.EmployeeId == userId);

            if (!isUserInProject && !isUserAssignedToTask && task.Project.ProjectManagerId != userId && task.Project.ClientId != userId)
            {
                return Forbid();
            }

            return View(task);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTask()
        {            
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }
            
            // Get projects managed by this project manager
            var projects = await _context.Projects
                .Where(p => p.ProjectManagerId == user.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.StartDate,
                    p.EndDate
                })
                .ToListAsync();

            // Get all active employees
            var employees = await _userManager.Users
                .Where(u => u.Role == "Employee" && !u.IsArchived)
                .ToListAsync();
            
            // Get task assignments including incomplete tasks for each employee
            var projectTaskAssignments = await _context.TaskAssignments
                .Include(ta => ta.ProjectTask)
                .Where(ta => ta.ProjectTask.Project.ProjectManagerId == user.Id)
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
                    text = e.UserName, // or e.Email or full name if available
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

            // Pass project dates to the view for date validation
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
                    var modelStateVal = ModelState[modelStateKey];
                    if (modelStateVal?.Errors != null)
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

            // Validate that the current user is the project manager
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null || project.ProjectManagerId != currentUser.Id)
            {
                ModelState.AddModelError(string.Empty, "You are not authorized to create tasks for this project");
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

        private async Task<IActionResult> PrepareCreateTaskViewModel(CreateTaskViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            // Fetch projects managed by the current Project Manager
            var projects = await _context.Projects
                .Where(p => p.ProjectManagerId != null && p.ProjectManagerId == currentUser.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    StartDate = p.StartDate.ToString("O").Substring(0, 10),
                    EndDate = p.EndDate.HasValue ? p.EndDate.Value.ToString("O").Substring(0, 10) : p.StartDate.AddMonths(1).ToString("O").Substring(0, 10)
                })
                .ToListAsync();

            // Get all active employees
            var employees = await _userManager.Users
                .Where(u => u.Role == "Employee" && !u.IsArchived)
                .ToListAsync();

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

            // Store project dates for JavaScript
            ViewBag.Projects = projects.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                startDate = p.StartDate,
                endDate = p.EndDate
            }).ToList();

            // Store full list of employees in ViewBag to filter dynamically via JavaScript
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmTask(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                    .ThenInclude(p => p.Tasks)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            // Verify the Project Manager is authorized for this task
            if (task.Project.ProjectManagerId != User.FindFirstValue(ClaimTypes.NameIdentifier))
            {
                return Forbid();
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

            // If task was overdue when completed, adjust other task timelines
            if (task.DueDate < DateTime.UtcNow)
            {
                _timelineAdjustmentService.AdjustTaskTimelines(task.Project.Tasks);
            }

            await _context.SaveChangesAsync();

            // Send notification
            await _notificationService.NotifyTaskUpdateAsync(task, $"Task '{task.Title}' has been marked as completed");

            TempData["SuccessMessage"] = "Task has been confirmed as completed.";
            return RedirectToAction(nameof(TaskDetails), new { id });
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

        [HttpPost]
        public async Task<IActionResult> ApproveTaskProof(int taskId)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || task.Project.ProjectManagerId != user.Id)
            {
                return Forbid();
            }

            task.IsConfirmed = true;
            task.Status = "Completed";
            task.CompletedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;

            // Create notification for employee
            var notification = new Notification
            {
                UserId = task.CreatedById,
                Title = "Task Proof Approved",
                Message = $"Your proof for task '{task.Title}' has been approved",
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                Link = $"/Employee/TaskDetails/{task.Id}"
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Task proof has been approved and marked as completed.";
            return RedirectToAction(nameof(TaskDetails), new { id = taskId });
        }

        public async Task<IActionResult> MyProjects()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            // Fetch projects managed by the current Project Manager
            var projects = await _context.Projects
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .Include(p => p.Tasks.Where(t => !t.IsArchived))
                .Where(p => p.ProjectManagerId == currentUser.Id && !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Calculate project progress
            var projectProgress = new Dictionary<int, int>();
            foreach (var project in projects)
            {
                var totalTasks = project.Tasks.Count;
                var completedTasks = project.Tasks.Count(t => t.Status == "Completed");
                projectProgress[project.Id] = totalTasks > 0
                    ? (int)Math.Round((double)completedTasks / totalTasks * 100)
                    : 0;
            }
            ViewBag.ProjectProgress = projectProgress;

            return View(projects);
        }

        [HttpGet]
        public async Task<IActionResult> ProjectDetails(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .Include(p => p.Tasks)
                    .ThenInclude(t => t.TaskAssignments)
                        .ThenInclude(ta => ta.Employee)
                .Include(p => p.ProjectTeams)
                    .ThenInclude(pt => pt.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            // Calculate project progress
            var totalTasks = project.Tasks.Count;
            var completedTasks = project.Tasks.Count(t => t.Status == "Completed");
            ViewBag.ProjectProgress = totalTasks > 0
                ? (int)Math.Round((double)completedTasks / totalTasks * 100)
                : 0;

            // Get member task counts
            var memberTaskCounts = new Dictionary<string, int>();
            foreach (var teamMember in project.ProjectTeams.Select(pt => pt.User))
            {
                var taskCount = project.Tasks
                    .Count(t => t.TaskAssignments.Any(ta => ta.EmployeeId == teamMember.Id));
                memberTaskCounts[teamMember.Id] = taskCount;
            }
            ViewBag.MemberTaskCounts = memberTaskCounts;

            return View(project);
        }

        [Authorize(Roles = "Project Manager")]
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
                Role = "Project Manager",
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                EmailNotificationsEnabled = user.EmailNotificationsEnabled
            };

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

            // Only allow the assigned project manager
            var currentUser = await _userManager.GetUserAsync(User);
            if (project.ProjectManagerId != currentUser?.Id)
            {
                return Forbid();
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
            return RedirectToAction(nameof(MyProjects));
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

            // Only allow the project manager
            var currentUser = await _userManager.GetUserAsync(User);
            if (task.Project.ProjectManagerId != currentUser?.Id)
            {
                return Forbid();
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

        [HttpGet]
        public async Task<IActionResult> ArchivedProjects()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            // Fetch archived projects managed by the current Project Manager
            var projects = await _context.Projects
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .Include(p => p.Tasks)
                .Where(p => p.ProjectManagerId == currentUser.Id && p.IsArchived)
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

            // Only allow the assigned project manager
            var currentUser = await _userManager.GetUserAsync(User);
            if (project.ProjectManagerId != currentUser?.Id)
            {
                return Forbid();
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
            await _notificationService.NotifyProjectUpdateAsync(project, $"Project '{project.Name}' has been unarchived");

            TempData["SuccessMessage"] = "Project has been unarchived successfully.";
            return RedirectToAction(nameof(ArchivedProjects));
        }

        [HttpGet]
        public async Task<IActionResult> ArchivedTasks()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            // Fetch archived tasks for projects managed by the current Project Manager
            var tasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Where(t => t.Project.ProjectManagerId == currentUser.Id && t.IsArchived)
                .OrderByDescending(t => t.ArchivedAt)
                .ToListAsync();

            return View(tasks);
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

            // Only allow the project manager
            var currentUser = await _userManager.GetUserAsync(User);
            if (task.Project.ProjectManagerId != currentUser?.Id)
            {
                return Forbid();
            }

            // Unarchive the task
            task.IsArchived = false;
            task.ArchivedAt = null;
            task.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify relevant parties
            await _notificationService.NotifyTaskUpdateAsync(task, $"Task '{task.Title}' has been unarchived");

            TempData["SuccessMessage"] = "Task has been unarchived successfully.";
            return RedirectToAction(nameof(ArchivedTasks));
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
    }
}