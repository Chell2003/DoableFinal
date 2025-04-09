using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;
using DoableFinal.ViewModels;

namespace DoableFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

        // Employee Management
        public async Task<IActionResult> Employees()
        {
            var employees = await _userManager.GetUsersInRoleAsync("Employee");
            return View(employees);
        }

        [HttpGet]
        public IActionResult CreateEmployee()
        {
            return View(new CreateEmployeeViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> CreateEmployee(CreateEmployeeViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Role = "Employee",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Employee");
                    TempData["SuccessMessage"] = "Employee account created successfully.";
                    return RedirectToAction(nameof(Users));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult CreateProjectManager()
        {
            return View(new CreateProjectManagerViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> CreateProjectManager(CreateProjectManagerViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Role = "Project Manager",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Project Manager");
                    TempData["SuccessMessage"] = "Project Manager account created successfully.";
                    return RedirectToAction(nameof(Users));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
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
                var project = new Project
                {
                    Name = model.Name,
                    Description = model.Description,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    Status = model.Status,
                    Priority = model.Priority,
                    ClientId = model.ClientId,
                    ProjectManagerId = model.ProjectManagerId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Project created successfully.";
                return RedirectToAction(nameof(Projects));
            }

            // Reload the select lists if we need to return to the view
            var clients = await _userManager.GetUsersInRoleAsync("Client");
            var projectManagers = await _userManager.GetUsersInRoleAsync("Project Manager");

            model.Clients = clients.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id,
                Text = $"{c.FirstName} {c.LastName}"
            }).ToList();

            model.ProjectManagers = projectManagers.Select(pm => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = pm.Id,
                Text = $"{pm.FirstName} {pm.LastName}"
            }).ToList();

            return View(model);
        }

        // Task Management
        public async Task<IActionResult> Tasks()
        {
            var tasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(tasks);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTask()
        {
            var projects = await _context.Projects.ToListAsync();
            var employees = await _userManager.GetUsersInRoleAsync("Employee");

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
        public async Task<IActionResult> CreateTask(CreateTaskViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);

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

                // Handle multiple task assignments
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

                        // Create task assignment
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

            // Reload the select lists if we need to return to the view
            var projects = await _context.Projects.ToListAsync();
            var employees = await _userManager.GetUsersInRoleAsync("Employee");

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

        // Users Management
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
            return View(users);
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
                Email = user.Email,
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
                Email = user.Email,
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
                user.Email = model.Email;
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

                project.Name = model.Name;
                project.Description = model.Description;
                project.StartDate = model.StartDate;
                project.EndDate = model.EndDate;
                project.Status = model.Status;
                project.ClientId = model.ClientId;
                project.ProjectManagerId = model.ProjectManagerId;
                project.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
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
        }

        [HttpGet]
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

            return View(task);
        }

        [HttpGet]
        public async Task<IActionResult> ProjectDetails(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Client)
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

            return View(project);
        }
    }
}