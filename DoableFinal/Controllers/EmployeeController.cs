using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using Microsoft.AspNetCore.Identity;
using Task = System.Threading.Tasks.Task;
using System.Security.Claims;
using System.Linq;

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

        public async Task<IActionResult> Notifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
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
                EmailNotificationsEnabled = user.EmailNotificationsEnabled,

                ResidentialAddress = user.ResidentialAddress ?? string.Empty,
                MobileNumber = user.MobileNumber ?? string.Empty,
                Birthday = user.Birthday,
                PagIbigAccount = user.PagIbigAccount ?? string.Empty,
                TinNumber = user.TinNumber ?? string.Empty,
                Position = user.Position ?? string.Empty
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            // If model validation fails, redirect back to Profile and show errors there
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["PasswordErrorMessage"] = string.IsNullOrWhiteSpace(errors) ? "Please correct the errors in the form." : errors;
                return RedirectToAction(nameof(Profile));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                var errors = string.Join(" ", changePasswordResult.Errors.Select(e => e.Description));
                TempData["PasswordErrorMessage"] = string.IsNullOrWhiteSpace(errors) ? "Failed to change password." : errors;
                return RedirectToAction(nameof(Profile));
            }

            // Success - show message on Profile page (which contains the change-password component)
            TempData["PasswordSuccessMessage"] = "Your password has been changed successfully.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
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
                if (!string.IsNullOrWhiteSpace(model.PagIbigAccount) && !pagIbigAttr.IsValid(model.PagIbigAccount))
                {
                    ModelState.AddModelError("PagIbigAccount", "Invalid Pag-IBIG MID. Expected 12 numeric digits.");
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

                // Employee / Project Manager fields
                user.ResidentialAddress = model.ResidentialAddress;
                user.Birthday = model.Birthday;
                user.PagIbigAccount = model.PagIbigAccount;
                user.TinNumber = model.TinNumber;
                user.MobileNumber = model.MobileNumber;
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
                    .ThenInclude(p => p.Tasks)
                .Include(pt => pt.Project)
                    .ThenInclude(p => p.ProjectManager)
                .Where(pt => pt.UserId == user.Id)
                .Select(pt => pt.Project)
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

        // Update Tasks method to use TaskAssignments
        public async Task<IActionResult> Tasks(string? q = "", string? statusFilter = "", string? fromDate = "", string? toDate = "")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var query = _context.TaskAssignments
                .Where(ta => ta.EmployeeId == user.Id)
                .Include(ta => ta.ProjectTask)
                    .ThenInclude(pt => pt.Project)
                .Select(ta => ta.ProjectTask)
                .Distinct()
                .AsQueryable();

            // Search by task title or project name
            if (!string.IsNullOrEmpty(q))
            {
                var searchTerm = q.ToLower();
                query = query.Where(t => 
                    t.Title.ToLower().Contains(searchTerm) ||
                    (t.Project != null && t.Project.Name.ToLower().Contains(searchTerm))
                );
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

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.SearchQuery = q;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.AvailableStatuses = new List<string> { "Open", "In Progress", "Resolved", "Closed" };
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
                .Include(t => t.Project)
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

                // If the task belongs to a project, ensure the project status is at least "In Progress"
                if (task.Project != null)
                {
                    if (!string.Equals(task.Project.Status, "In Progress", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(task.Project.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        task.Project.Status = "In Progress";
                        task.Project.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();

                TempData["TaskMessage"] = "Task submitted for review successfully. Waiting for Project Manager confirmation.";
            }
            else
            {
                TempData["ErrorMessage"] = "Please upload a valid file.";
            }

            return RedirectToAction(nameof(TaskDetails), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitTaskProof(int taskId, IFormFile proofFile, string remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks))
            {
                TempData["Error"] = "Please provide remarks about the task completion.";
                return RedirectToAction(nameof(TaskDetails), new { id = taskId });
            }

            var task = await _context.Tasks
                .Include(t => t.TaskAssignments)
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || task.TaskAssignments?.Any(ta => ta.EmployeeId == user.Id) != true)
            {
                return Forbid();
            }

            if (proofFile != null && proofFile.Length > 0)
            {
                // Create the uploads directory if it doesn't exist
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "proofs");
                Directory.CreateDirectory(uploadsFolder);

                // Generate a unique filename
                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(proofFile.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await proofFile.CopyToAsync(stream);
                }

                // Update the task proof file path
                task.ProofFilePath = $"/uploads/proofs/{uniqueFileName}";
            }

            // Update the task
            task.Remarks = remarks;
            task.Status = "Pending Approval";
            task.UpdatedAt = DateTime.UtcNow;

            // If the task belongs to a project, ensure the project status is at least "In Progress"
            if (task.Project != null)
            {
                if (!string.Equals(task.Project.Status, "In Progress", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(task.Project.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    task.Project.Status = "In Progress";
                    task.Project.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            // Create notification for project manager
            var notification = new Notification
            {
                UserId = task.Project.ProjectManagerId,
                Title = "Task Proof Submitted",
                Message = $"New proof submitted for task: {task.Title}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                Link = $"/ProjectManager/TaskDetails/{task.Id}"
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Proof has been submitted for approval.";
            return RedirectToAction(nameof(TaskDetails), new { id = taskId });
        }

        public async Task<IActionResult> DownloadProof(int taskId)
        {
            var task = await _context.Tasks
                .Include(t => t.TaskAssignments)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null || string.IsNullOrEmpty(task.ProofFilePath))
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || task.TaskAssignments?.Any(ta => ta.EmployeeId == user.Id) != true)
            {
                return Forbid();
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", task.ProofFilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var fileName = Path.GetFileName(filePath);
            var mimeType = "application/octet-stream";
            return PhysicalFile(filePath, mimeType, fileName);
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

            TempData["TaskMessage"] = "Comment posted successfully.";
            return RedirectToAction("TaskDetails", new { id = taskId });
        }

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

            // Check if the current user is part of the project team
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isTeamMember = project.ProjectTeams.Any(pt => pt.UserId == userId);

            if (!isTeamMember)
            {
                return Forbid();
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

            ViewBag.CurrentUserId = userId;
            return View(project);
        }

        public async Task<IActionResult> TaskDetails(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                    .ThenInclude(p => p.ProjectTeams)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Include(t => t.Comments)
                    .ThenInclude(c => c.CreatedBy)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
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

            return View(task);
        }
    }
}