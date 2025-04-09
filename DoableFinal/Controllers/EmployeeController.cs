using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using Microsoft.AspNetCore.Identity;
using Task = System.Threading.Tasks.Task;
using System.Security.Claims;

namespace DoableFinal.Controllers
{
    [Authorize(Roles = "Employee")]
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Get task statistics using TaskAssignments
            ViewBag.TotalTasks = await _context.TaskAssignments
                .Where(ta => ta.EmployeeId == user.Id)
                .Select(ta => ta.ProjectTaskId)
                .Distinct()
                .CountAsync();

            ViewBag.CompletedTasks = await _context.TaskAssignments
                .Where(ta => ta.EmployeeId == user.Id)
                .Join(_context.Tasks,
                      ta => ta.ProjectTaskId,
                      t => t.Id,
                      (ta, t) => new { Task = t })
                .Where(x => x.Task.Status == "Completed")
                .Select(x => x.Task.Id)
                .Distinct()
                .CountAsync();

            ViewBag.OverdueTasks = await _context.TaskAssignments
                .Where(ta => ta.EmployeeId == user.Id)
                .Join(_context.Tasks,
                      ta => ta.ProjectTaskId,
                      t => t.Id,
                      (ta, t) => new { Task = t })
                .Where(x => x.Task.Status != "Completed" && x.Task.DueDate < DateTime.UtcNow)
                .Select(x => x.Task.Id)
                .Distinct()
                .CountAsync();

            // Get project count
            ViewBag.ProjectCount = await _context.ProjectTeams
                .Where(pt => pt.UserId == user.Id)
                .CountAsync();

            // Get assigned tasks
            ViewBag.MyTasks = await _context.TaskAssignments
                .Where(ta => ta.EmployeeId == user.Id)
                .Include(ta => ta.ProjectTask)
                    .ThenInclude(pt => pt.Project)
                .Select(ta => ta.ProjectTask)
                .Distinct()
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Get assigned projects
            ViewBag.MyProjects = await _context.ProjectTeams
                .Include(pt => pt.Project)
                .Where(pt => pt.UserId == user.Id)
                .Select(pt => pt.Project)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Get recent activity
            var recentActivity = new List<dynamic>();

            // Add recent task updates
            var recentTasks = await _context.TaskAssignments
                .Where(ta => ta.EmployeeId == user.Id)
                .Include(ta => ta.ProjectTask)
                    .ThenInclude(pt => pt.Project)
                .Select(ta => ta.ProjectTask)
                .Distinct()
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .Take(5)
                .ToListAsync();

            foreach (var task in recentTasks)
            {
                recentActivity.Add(new
                {
                    Title = $"Task: {task.Title}",
                    Description = $"Status updated to {task.Status}",
                    Timestamp = task.UpdatedAt ?? task.CreatedAt
                });
            }

            // Add recent project updates
            var recentProjects = await _context.ProjectTeams
                .Include(pt => pt.Project)
                .Where(pt => pt.UserId == user.Id)
                .OrderByDescending(pt => pt.Project.UpdatedAt ?? pt.Project.CreatedAt)
                .Take(5)
                .Select(pt => pt.Project)
                .ToListAsync();

            foreach (var project in recentProjects)
            {
                recentActivity.Add(new
                {
                    Title = $"Project: {project.Name}",
                    Description = $"Status updated to {project.Status}",
                    Timestamp = project.UpdatedAt ?? project.CreatedAt
                });
            }

            // Sort all activity by timestamp
            ViewBag.RecentActivity = recentActivity
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToList();

            return View();
        }

        // Profile method remains the same
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
                Role = "Employee",
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                EmailNotificationsEnabled = user.EmailNotificationsEnabled
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

        // Projects method remains the same
        public async Task<IActionResult> Projects()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var projects = await _context.ProjectTeams
                .Include(pt => pt.Project)
                .Where(pt => pt.UserId == user.Id)
                .Select(pt => pt.Project)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(projects);
        }

        // Update Tasks method to use TaskAssignments
        public async Task<IActionResult> Tasks()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var tasks = await _context.TaskAssignments
                .Where(ta => ta.EmployeeId == user.Id)
                .Include(ta => ta.ProjectTask)
                    .ThenInclude(pt => pt.Project)
                .Select(ta => ta.ProjectTask)
                .Distinct()
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tasks);
        }

        [HttpGet]
        public async Task<IActionResult> UpdateTaskStatus(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Check if user is assigned to this task
            var isUserAssigned = await _context.TaskAssignments
                .AnyAsync(ta => ta.ProjectTaskId == id && ta.EmployeeId == user.Id);

            if (!isUserAssigned)
            {
                return NotFound();
            }

            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            var model = new UpdateTaskStatusViewModel
            {
                Id = task.Id,
                Status = task.Status
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTaskStatus(int id, UpdateTaskStatusViewModel model)
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

            // Check if user is assigned to this task
            var isUserAssigned = await _context.TaskAssignments
                .AnyAsync(ta => ta.ProjectTaskId == id && ta.EmployeeId == user.Id);

            if (!isUserAssigned)
            {
                return NotFound();
            }

            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            task.Status = model.Status;
            task.UpdatedAt = DateTime.UtcNow;

            if (model.Status == "Completed")
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Task status updated successfully.";
            return RedirectToAction(nameof(Tasks));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTaskStatusForm(int id, UpdateTaskStatusViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("UpdateTaskStatus", model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Check if user is assigned to this task
            var isUserAssigned = await _context.TaskAssignments
                .AnyAsync(ta => ta.ProjectTaskId == id && ta.EmployeeId == user.Id);

            if (!isUserAssigned)
            {
                return NotFound();
            }

            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            task.Status = model.Status;
            task.UpdatedAt = DateTime.UtcNow;

            if (model.Status == "Completed")
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Task status updated successfully.";
            return RedirectToAction(nameof(Tasks));
        }

        public async Task<IActionResult> ProjectDetails(int id)
        {
            var project = await _context.Projects
                .Include(p => p.ProjectManager)
                .Include(p => p.ProjectTeams)
                    .ThenInclude(pt => pt.User)
                .Include(p => p.Tasks)
                    .ThenInclude(t => t.TaskAssignments)
                        .ThenInclude(ta => ta.Employee)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            // Check if the current user is part of the project team
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isTeamMember = project.ProjectTeams.Any(pt => pt.UserId == userId);

            if (!isTeamMember)
            {
                return Forbid();
            }

            ViewBag.CurrentUserId = userId;
            return View(project);
        }

        public async Task<IActionResult> TaskDetails(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Include(t => t.CreatedBy)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            // Check if the current user is assigned to this task
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isUserAssigned = task.TaskAssignments.Any(ta => ta.EmployeeId == userId);

            if (!isUserAssigned)
            {
                return Forbid();
            }

            return View(task);
        }
    }
}