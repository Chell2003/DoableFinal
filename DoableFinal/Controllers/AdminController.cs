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
        private readonly TimelineAdjustmentService _timelineAdjustmentService;
        private readonly NotificationService _notificationService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, TimelineAdjustmentService timelineAdjustmentService, NotificationService notificationService, ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _timelineAdjustmentService = timelineAdjustmentService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // Get counts for dashboard statistics
            ViewBag.TotalProjects = await _context.Projects.CountAsync();
            ViewBag.TotalTasks = await _context.Tasks.CountAsync();
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.CompletedTasks = await _context.Tasks.CountAsync(t => t.Status == "Completed");
            ViewBag.OverdueTasks = await _context.Tasks.CountAsync(t => t.Status != "Completed" && t.DueDate < DateTime.UtcNow);

            // Get recent users
            ViewBag.RecentUsers = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Get recent projects
            ViewBag.RecentProjects = await _context.Projects
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Get recent tasks - Update to handle multiple assignments
            ViewBag.RecentTasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View();
        }

        // Consolidated User Management
        public async Task<IActionResult> Users(string roleFilter = "")
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            if (!string.IsNullOrEmpty(roleFilter))
            {
                users = users.Where(u => u.Role == roleFilter).ToList();
            }

            ViewBag.RoleFilter = roleFilter;
            return View(users);
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
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
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

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                    TempData["SuccessMessage"] = $"{model.Role} account created successfully.";
                    return RedirectToAction(nameof(Users));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
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
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                await _userManager.UpdateAsync(user);
                TempData["SuccessMessage"] = $"User status updated to {(user.IsActive ? "active" : "inactive")}.";
            }
            return RedirectToAction(nameof(Users));
        }

        // Project Management
        public async Task<IActionResult> Projects()
        {
            var projects = await _context.Projects
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return View(projects);
        }

        [HttpGet]
        public async Task<IActionResult> CreateProject()
        {
            var clients = await _userManager.GetUsersInRoleAsync("Client");
            var projectManagers = await _userManager.GetUsersInRoleAsync("Project Manager");

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
            var clients = await _userManager.GetUsersInRoleAsync("Client");
            var projectManagers = await _userManager.GetUsersInRoleAsync("Project Manager");

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
                .AsQueryable();

            // Apply filters
            query = filter switch
            {
                "pending" => query.Where(t => !string.IsNullOrEmpty(t.ProofFilePath) && !t.IsConfirmed && t.Status != "Completed"),
                "completed" => query.Where(t => t.Status == "Completed"),
                "in-progress" => query.Where(t => t.Status == "In Progress"),
                _ => query
            };

            var tasks = await query
                .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
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
                FirstName = user.FirstName,
                LastName = user.LastName,
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
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "New password and confirmation password do not match.";
                return RedirectToAction(nameof(Profile));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Password changed successfully.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                TempData["ErrorMessage"] = error.Description;
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
                FirstName = user.FirstName,
                LastName = user.LastName,
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

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email ?? string.Empty;
                user.IsActive = model.IsActive;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    // Update role if changed
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    if (!currentRoles.Contains(model.Role))
                    {
                        await _userManager.RemoveFromRolesAsync(user, currentRoles);
                        await _userManager.AddToRoleAsync(user, model.Role);
                    }

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
                ProjectManagerId = project.ProjectManagerId
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

                var oldStatus = project.Status;
                project.Name = model.Name;
                project.Description = model.Description;
                project.StartDate = model.StartDate;
                project.EndDate = model.EndDate;
                project.Status = model.Status;
                project.ClientId = model.ClientId;
                project.ProjectManagerId = model.ProjectManagerId;
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
        public async Task<IActionResult> DeleteProject(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Project deleted successfully.";
            return RedirectToAction(nameof(Projects));
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
        public async Task<IActionResult> DeleteTask(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null)
            {
                return NotFound();
            }

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Task deleted successfully.";
            return RedirectToAction(nameof(Tasks));
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
            // Get all projects
            var projects = await _context.Projects
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.StartDate,
                    p.EndDate
                })
                .ToListAsync();

            // Get all employees
            var employees = await _userManager.GetUsersInRoleAsync("Employee");
            
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
    }
}