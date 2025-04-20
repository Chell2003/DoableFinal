using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using System.Security.Claims;

namespace DoableFinal.Controllers
{
    [Authorize(Roles = "Project Manager")]
    public class ProjectManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TimelineAdjustmentService _timelineAdjustmentService;
        private readonly ILogger<ProjectManagerController> _logger;

        public ProjectManagerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, TimelineAdjustmentService timelineAdjustmentService, ILogger<ProjectManagerController> logger)
        {
            _context = context;
            _userManager = userManager;
            _timelineAdjustmentService = timelineAdjustmentService;
            _logger = logger;
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
                .Where(p => p.ProjectManagerId != null && p.ProjectManagerId == userId)
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
                .Where(t => t.Project != null && t.Project.ProjectManagerId != null && t.Project.ProjectManagerId == currentUser.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tasks);
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

            // Check if the user is part of the project team or assigned to the task
            var isUserInProject = task.Project.ProjectTeams?.Any(pt => pt.UserId == userId) ?? false;
            var isUserAssignedToTask = task.TaskAssignments?.Any(ta => ta.EmployeeId == userId) ?? false;

            if (!isUserInProject && !isUserAssignedToTask && task.Project.ProjectManagerId != userId && task.Project.ClientId != userId)
            {
                return Forbid();
            }

            return View(task);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTask()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            // Fetch projects managed by the current Project Manager
            var projects = await _context.Projects
                .Where(p => p.ProjectManagerId != null && p.ProjectManagerId == currentUser.Id)
                .ToListAsync();

            // Fetch employees assigned to the projects managed by the Project Manager
            var employees = await _context.ProjectTeams
                .Include(pt => pt.User)
                .Where(pt => projects.Select(p => p.Id).Contains(pt.ProjectId))
                .Select(pt => pt.User)
                .Distinct()
                .ToListAsync();

            var viewModel = new CreateTaskViewModel
            {
                Projects = projects.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                }).ToList(),

                Employees = employees.Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = e.Id,
                    Text = $"{e.FirstName} {e.LastName}"
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTask(CreateTaskViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.Id == null)
                {
                    ModelState.AddModelError(string.Empty, "Unable to identify the current user.");
                    return View(model);
                }

                var task = new ProjectTask
                {
                    Title = model.Title,
                    Description = model.Description,
                    StartDate = model.StartDate,
                    DueDate = model.DueDate,
                    Status = model.Status,
                    Priority = model.Priority,
                    ProjectId = model.ProjectId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = currentUser.Id
                };

                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();

                // Add assignments if specified
                if (model.AssignedToIds != null && model.AssignedToIds.Any())
                {
                    foreach (var employeeId in model.AssignedToIds)
                    {
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

                TempData["SuccessMessage"] = "Task created successfully.";
                return RedirectToAction(nameof(Tasks));
            }

            // Reload dropdowns if validation fails
            var currentUserReload = await _userManager.GetUserAsync(User);
            if (currentUserReload?.Id == null)
            {
                return NotFound();
            }

            var projects = await _context.Projects
                .Where(p => p.ProjectManagerId != null && p.ProjectManagerId == currentUserReload.Id)
                .ToListAsync();

            // Get list of employees
            var employees = await _context.ProjectTeams
                .Include(pt => pt.User)
                .Where(pt => pt.Project != null && projects.Select(p => p.Id).Contains(pt.ProjectId))
                .Select(pt => pt.User)
                .Distinct()
                .ToListAsync();

            model.Projects = projects.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Name
            }).ToList();

            model.Employees = employees.Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = e.Id,
                Text = $"{e.FirstName} {e.LastName}"
            }).ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmTask(int id)
        {
            var task = await _context.Tasks.FindAsync(id);

            if (task == null)
            {
                return NotFound();
            }

            // Confirm the task
            task.Status = "Completed";
            task.IsConfirmed = true;
            task.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Task confirmed successfully.";
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
                .Include(t => t.Project.ProjectTeams)
                .Include(t => t.TaskAssignments)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                TempData["ErrorMessage"] = "Task not found.";
                return RedirectToAction("Tasks");
            }

            // Check if the user is part of the project team or assigned to the task
            var isUserInProject = task.Project.ProjectTeams.Any(pt => pt.UserId == currentUser.Id);
            var isUserAssignedToTask = task.TaskAssignments.Any(ta => ta.EmployeeId == currentUser.Id);

            if (!isUserInProject && !isUserAssignedToTask && task.Project.ProjectManagerId != currentUser.Id && task.Project.ClientId != currentUser.Id)
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

        public async Task<IActionResult> MyProjects()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == null)
            {
                return NotFound();
            }

            // Fetch projects assigned to the current Project Manager
            var projects = await _context.Projects
                .Where(p => p.ProjectManagerId != null && p.ProjectManagerId == currentUser.Id)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

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
                .Include(p => p.ProjectTeams)
                    .ThenInclude(pt => pt.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound(); // Handle the case where the project is not found
            }

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
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                Role = "Project Manager",
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                EmailNotificationsEnabled = user.EmailNotificationsEnabled
            };

            return View(model);
        }
    }
}