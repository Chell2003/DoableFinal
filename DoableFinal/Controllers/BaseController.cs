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
    public class BaseController : Controller
    {
        protected readonly ApplicationDbContext _context;
        protected readonly UserManager<ApplicationUser> _userManager;
        protected readonly NotificationService _notificationService;
        protected readonly TimelineAdjustmentService _timelineAdjustmentService;

        public BaseController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService,
            TimelineAdjustmentService timelineAdjustmentService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _timelineAdjustmentService = timelineAdjustmentService;
        }

        protected async Task<ApplicationUser> GetCurrentUser()
        {
            return await _userManager.GetUserAsync(User);
        }

        protected string GetCurrentRole()
        {
            return User.IsInRole("Admin") ? "Admin" :
                   (User.IsInRole("Project Manager") || User.IsInRole("ProjectManager")) ? "Project Manager" :
                   User.IsInRole("Employee") ? "Employee" : "Client";
        }

        protected async Task<int> GetProjectCount(string userId, string role)
        {
            return role switch
            {
                "Admin" => await _context.Projects.CountAsync(p => !p.IsArchived),
                "Project Manager" => await _context.Projects.CountAsync(p => p.ProjectManagerId == userId && !p.IsArchived),
                "Client" => await _context.Projects.CountAsync(p => p.ClientId == userId && !p.IsArchived),
                "Employee" => await _context.ProjectTeams.Where(pt => pt.UserId == userId && !pt.Project.IsArchived).Select(pt => pt.ProjectId).Distinct().CountAsync(),
                _ => 0
            };
        }

        protected async Task<int> GetTaskCount(string userId, string role)
        {
            return role switch
            {
                "Admin" => await _context.Tasks.CountAsync(t => !t.IsArchived),
                "Project Manager" => await _context.Tasks.CountAsync(t => 
                    (t.Project.ProjectManagerId == userId || 
                     t.TaskAssignments.Any(ta => ta.EmployeeId == userId)) && 
                    !t.IsArchived),
                "Client" => await _context.Tasks.CountAsync(t => 
                    t.Project.ClientId == userId && 
                    !t.IsArchived),
                "Employee" => await _context.TaskAssignments.CountAsync(ta => 
                    ta.EmployeeId == userId && 
                    !ta.ProjectTask.IsArchived),
                _ => 0
            };
        }

        protected async Task<IEnumerable<Project>> GetProjects(string userId, string role, int take = 5)
        {
            var query = _context.Projects
                .Include(p => p.Client)
                .Include(p => p.ProjectManager)
                .Include(p => p.Tasks)
                .Where(p => !p.IsArchived);

            query = role switch
            {
                "Admin" => query,
                "Project Manager" => query.Where(p => p.ProjectManagerId == userId),
                "Client" => query.Where(p => p.ClientId == userId),
                "Employee" => query.Where(p => p.ProjectTeams.Any(pt => pt.UserId == userId)),
                _ => query.Where(p => false)
            };

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        protected async Task<IEnumerable<ProjectTask>> GetTasks(string userId, string role, int take = 5)
        {
            var query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskAssignments)
                    .ThenInclude(ta => ta.Employee)
                .Where(t => !t.IsArchived);

            query = role switch
            {
                "Admin" => query,
                "Project Manager" => query.Where(t => 
                    t.Project.ProjectManagerId == userId || 
                    t.TaskAssignments.Any(ta => ta.EmployeeId == userId)),
                "Client" => query.Where(t => t.Project.ClientId == userId),
                "Employee" => query.Where(t => t.TaskAssignments.Any(ta => ta.EmployeeId == userId)),
                _ => query.Where(t => false)
            };

            return await query
                .OrderByDescending(t => t.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        protected async Task<Dictionary<int, int>> GetProjectProgress(IEnumerable<Project> projects)
        {
            var progress = new Dictionary<int, int>();
            foreach (var project in projects)
            {
                var totalTasks = project.Tasks.Count;
                var completedTasks = project.Tasks.Count(t => t.Status == "Completed");
                progress[project.Id] = totalTasks > 0
                    ? (int)Math.Round((double)completedTasks / totalTasks * 100)
                    : 0;
            }
            return progress;
        }

        protected async Task<Dictionary<string, List<Notification>>> GetNotifications(string userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return new Dictionary<string, List<Notification>>
            {
                { "Unread", notifications.Where(n => !n.IsRead).ToList() },
                { "Read", notifications.Where(n => n.IsRead).ToList() }
            };
        }

        protected async Task<bool> ValidateAccess(int projectId, string userId, string role)
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return false;

            return role switch
            {
                "Admin" => true,
                "Project Manager" => project.ProjectManagerId == userId,
                "Client" => project.ClientId == userId,
                "Employee" => await _context.ProjectTeams.AnyAsync(pt => 
                    pt.ProjectId == projectId && pt.UserId == userId),
                _ => false
            };
        }
    }
}