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

            // Get statistics
            ViewBag.MyProjects = await _context.Projects
                .Where(p => p.ProjectManagerId == currentUser.Id)
                .CountAsync();

            ViewBag.MyTasks = await _context.Tasks
                .Where(t => t.Project.ProjectManagerId == currentUser.Id)
                .CountAsync();

            ViewBag.OverdueTasks = await _context.Tasks
                .Where(t => t.Project.ProjectManagerId == currentUser.Id &&
                            t.DueDate < DateTime.UtcNow &&
                            t.Status != "Completed")
                .CountAsync();

            // Get recent projects
            ViewBag.RecentProjects = await _context.Projects
                .Where(p => p.ProjectManagerId == currentUser.Id)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Get team members
            ViewBag.TeamMembers = await _context.ProjectTeams
                .Include(pt => pt.User)
                .Where(pt => pt.Project.ProjectManagerId == currentUser.Id)
                .Select(pt => pt.User)
                .Distinct()
                .ToListAsync();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Tasks()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return NotFound();
            }

            // Fetch all tasks for projects managed by the current Project Manager
            var tasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Where(t => t.Project.ProjectManagerId == currentUser.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tasks);
        }

        [Authorize(Roles = "Project Manager")]
        public async Task<IActionResult> TaskDetails(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .FirstOrDefaultAsync(t => t.Id == id && t.Project.ProjectManagerId == userId);

            if (task == null)
            {
                return NotFound();
            }

            return View(task);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTask()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // Fetch projects managed by the current Project Manager
            var projects = await _context.Projects
                .Where(p => p.ProjectManagerId == currentUser.Id)
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
                // Fetch the project to validate the dates
                var project = await _context.Projects.FindAsync(model.ProjectId);
                if (project == null)
                {
                    ModelState.AddModelError(string.Empty, "The selected project does not exist.");
                }
                else
                {
                    // Validate task dates against project dates
                    if (model.StartDate < project.StartDate || model.DueDate > project.EndDate)
                    {
                        ModelState.AddModelError(string.Empty, $"Task dates must be within the project's start and end dates: {project.StartDate:yyyy-MM-dd} to {project.EndDate:yyyy-MM-dd}.");
                    }
                }

                if (ModelState.IsValid)
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
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Tasks.Add(task);
                    await _context.SaveChangesAsync();

                    // Assign employees to the task
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
                    return RedirectToAction(nameof(Index));
                }
            }

            // Reload dropdowns if validation fails
            var currentUser = await _userManager.GetUserAsync(User);
            var projects = await _context.Projects
                .Where(p => p.ProjectManagerId == currentUser.Id)
                .ToListAsync();

            var employeesWithIncompleteTasks = await _context.TaskAssignments
                .Where(ta => ta.ProjectTask.Status != "Completed")
                .Select(ta => ta.EmployeeId)
                .Distinct()
                .ToListAsync();

            var employees = await _context.ProjectTeams
                .Include(pt => pt.User)
                .Where(pt => projects.Select(p => p.Id).Contains(pt.ProjectId))
                .Select(pt => pt.User)
                .Distinct()
                .ToListAsync();

            var assignableEmployees = employees
                .Where(e => !employeesWithIncompleteTasks.Contains(e.Id))
                .ToList();

            model.Projects = projects.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Name
            }).ToList();

            model.Employees = assignableEmployees.Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
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

        public async Task<IActionResult> MyProjects()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // Fetch projects assigned to the current Project Manager
            var projects = await _context.Projects
                .Where(p => p.ProjectManagerId == currentUser.Id)
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
                Email = user.Email,
                Role = "Project Manager",
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                EmailNotificationsEnabled = user.EmailNotificationsEnabled
            };

            return View(model);
        }
    }
}