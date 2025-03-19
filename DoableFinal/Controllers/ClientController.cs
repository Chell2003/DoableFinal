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
    [Authorize(Roles = "Client")]
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClientController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // Get statistics
            ViewBag.ProjectCount = await _context.Projects
                .Where(p => p.ClientId == currentUser.Id)
                .CountAsync();

            ViewBag.TotalTasks = await _context.Tasks
                .Where(t => t.Project.ClientId == currentUser.Id)
                .CountAsync();

            ViewBag.CompletedTasks = await _context.Tasks
                .Where(t => t.Project.ClientId == currentUser.Id && t.Status == "Completed")
                .CountAsync();

            ViewBag.OverdueTasks = await _context.Tasks
                .Where(t => t.Project.ClientId == currentUser.Id && 
                           t.DueDate < DateTime.UtcNow && 
                           t.Status != "Completed")
                .CountAsync();

            // Get recent projects
            ViewBag.MyProjects = await _context.Projects
                .Include(p => p.ProjectManager)
                .Where(p => p.ClientId == currentUser.Id)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Calculate project progress
            ViewBag.ProjectProgress = await GetProjectProgress(ViewBag.MyProjects);

            // Get project team members
            ViewBag.ProjectTeam = await _context.ProjectTeams
                .Include(pt => pt.User)
                .Where(pt => pt.Project.ClientId == currentUser.Id)
                .Select(pt => pt.User)
                .Distinct()
                .Take(5)
                .ToListAsync();

            // Get member task counts
            ViewBag.MemberTaskCounts = await GetMemberTaskCounts(ViewBag.ProjectTeam);

            // Get recent tasks
            ViewBag.ProjectTasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Where(t => t.Project.ClientId == currentUser.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View();
        }

        public async Task<IActionResult> Projects()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var projects = await _context.Projects
                .Include(p => p.ProjectManager)
                .Where(p => p.ClientId == currentUser.Id)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.ProjectProgress = await GetProjectProgress(projects);

            return View(projects);
        }

        public async Task<IActionResult> ProjectDetails(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);

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

            // Get recent tasks
            ViewBag.RecentTasks = await _context.Tasks
                .Include(t => t.AssignedTo)
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
                TempData["SuccessMessage"] = "Profile updated successfully.";
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
                .Include(t => t.AssignedTo)
                .Include(t => t.CreatedBy)
                .Where(t => t.Project.ClientId == userId);

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

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tasks);
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
                var taskCount = await _context.Tasks
                    .Where(t => t.AssignedToId == member.Id)
                    .CountAsync();
                counts[member.Id] = taskCount;
            }
            return counts;
        }
    }
} 