using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;
using DoableFinal.Services;

namespace DoableFinal.Controllers
{
    [Authorize]
    public class DashboardController : BaseController
    {
        public DashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService,
            TimelineAdjustmentService timelineAdjustmentService)
            : base(context, userManager, notificationService, timelineAdjustmentService)
        {
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await GetCurrentUser();
            var userRole = GetCurrentRole();

            // Common statistics
            ViewBag.UserRole = userRole;
            ViewBag.CurrentUser = currentUser;
            ViewBag.ProjectCount = await GetProjectCount(currentUser.Id, userRole);
            ViewBag.TaskCount = await GetTaskCount(currentUser.Id, userRole);
            ViewBag.OverdueTasks = await _context.Tasks
                .Where(t => t.DueDate < DateTime.UtcNow && t.Status != "Completed" && !t.IsArchived)
                .CountAsync();

            // Role-specific statistics and data
            if (userRole == "Admin")
            {
                ViewBag.TotalUsers = await _context.Users.CountAsync();
                ViewBag.RecentUsers = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(5)
                    .ToListAsync();
            }

            // Common data for all roles
            var projects = await GetProjects(currentUser.Id, userRole);
            ViewBag.Projects = projects;
            ViewBag.ProjectProgress = await GetProjectProgress(projects);
            ViewBag.Tasks = await GetTasks(currentUser.Id, userRole);
            ViewBag.Notifications = await GetNotifications(currentUser.Id);

            // Team members or project team based on role
            if (userRole == "Project Manager")
            {
                ViewBag.TeamMembers = await _context.ProjectTeams
                    .Include(pt => pt.User)
                    .Where(pt => pt.Project.ProjectManagerId == currentUser.Id)
                    .Select(pt => pt.User)
                    .Distinct()
                    .Take(5)
                    .ToListAsync();
            }
            else if (userRole == "Client")
            {
                ViewBag.ProjectTeam = await _context.ProjectTeams
                    .Include(pt => pt.User)
                    .Where(pt => pt.Project.ClientId == currentUser.Id)
                    .Select(pt => pt.User)
                    .Distinct()
                    .Take(5)
                    .ToListAsync();
            }

            return View();
        }
    }
}