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
        private readonly TimelineAdjustmentService _timelineAdjustmentService;

        public EmployeeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, TimelineAdjustmentService timelineAdjustmentService)
        {
            _context = context;
            _userManager = userManager;
            _timelineAdjustmentService = timelineAdjustmentService;
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
                Email = user.Email ?? string.Empty,
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProof(int id, IFormFile proofFile)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Check if the user is assigned to the task
            var task = await _context.Tasks
                .Include(t => t.TaskAssignments)
                .FirstOrDefaultAsync(t => t.Id == id && t.TaskAssignments.Any(ta => ta.EmployeeId == user.Id));

            if (task == null)
            {
                return NotFound();
            }

            if (proofFile != null && proofFile.Length > 0)
            {
                // Save the file to the server
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsFolder); // Ensure the folder exists
                var filePath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}_{proofFile.FileName}");

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await proofFile.CopyToAsync(stream);
                }

                // Update the task with the file path and mark it as "For Review"
                task.ProofFilePath = $"/uploads/{Path.GetFileName(filePath)}";
                task.Status = "For Review";
                task.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Task submitted for review successfully. Waiting for Project Manager confirmation.";
            }
            else
            {
                TempData["ErrorMessage"] = "Please upload a valid file.";
            }

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

            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null)
            {
                TempData["ErrorMessage"] = "Task not found.";
                return RedirectToAction("Tasks");
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
                .Include(t => t.Comments)
                    .ThenInclude(c => c.CreatedBy) // Include comment creator details
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null) return NotFound();

            return View(task);
        }
    }
}