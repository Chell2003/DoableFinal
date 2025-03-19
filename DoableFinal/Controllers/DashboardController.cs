using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;

namespace DoableFinal.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);

            if (User.IsInRole("Admin"))
            {
                // Get statistics for Admin
                ViewBag.TotalProjects = await _context.Projects.CountAsync();
                ViewBag.TotalTasks = await _context.Tasks.CountAsync();
                ViewBag.TotalUsers = await _context.Users.CountAsync();
                ViewBag.OverdueTasks = await _context.Tasks
                    .Where(t => t.DueDate < DateTime.UtcNow && t.Status != "Completed")
                    .CountAsync();

                // Get recent projects
                ViewBag.RecentProjects = await _context.Projects
                    .Include(p => p.Client)
                    .Include(p => p.ProjectManager)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                // Get recent tasks
                ViewBag.RecentTasks = await _context.Tasks
                    .Include(t => t.Project)
                    .Include(t => t.AssignedTo)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                // Get recent users
                ViewBag.RecentUsers = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                return View("Admin");
            }
            else if (User.IsInRole("Project Manager"))
            {
                // Get statistics for Project Manager
                ViewBag.MyProjects = await _context.Projects
                    .Where(p => p.ProjectManagerId == currentUser.Id)
                    .CountAsync();
                ViewBag.MyTasks = await _context.Tasks
                    .Where(t => t.AssignedToId == currentUser.Id)
                    .CountAsync();
                ViewBag.TeamMembers = await _context.ProjectTeams
                    .Where(pt => pt.Project.ProjectManagerId == currentUser.Id)
                    .Select(pt => pt.UserId)
                    .Distinct()
                    .CountAsync();
                ViewBag.OverdueTasks = await _context.Tasks
                    .Where(t => t.DueDate < DateTime.UtcNow && t.Status != "Completed" && 
                               (t.AssignedToId == currentUser.Id || 
                                t.Project.ProjectManagerId == currentUser.Id))
                    .CountAsync();

                // Get my projects
                ViewBag.MyProjects = await _context.Projects
                    .Include(p => p.Client)
                    .Include(p => p.ProjectManager)
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
                    .Take(5)
                    .ToListAsync();

                // Get my tasks
                ViewBag.MyTasks = await _context.Tasks
                    .Include(t => t.Project)
                    .Include(t => t.AssignedTo)
                    .Where(t => t.AssignedToId == currentUser.Id || 
                               t.Project.ProjectManagerId == currentUser.Id)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                return View("ProjectManager");
            }
            else // Client
            {
                // Get statistics for Client
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
                    .Where(t => t.DueDate < DateTime.UtcNow && t.Status != "Completed" && 
                               t.Project.ClientId == currentUser.Id)
                    .CountAsync();

                // Get my projects
                ViewBag.MyProjects = await _context.Projects
                    .Include(p => p.Client)
                    .Include(p => p.ProjectManager)
                    .Where(p => p.ClientId == currentUser.Id)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                // Get project team
                ViewBag.ProjectTeam = await _context.ProjectTeams
                    .Include(pt => pt.User)
                    .Where(pt => pt.Project.ClientId == currentUser.Id)
                    .Select(pt => pt.User)
                    .Distinct()
                    .Take(5)
                    .ToListAsync();

                // Get project tasks
                ViewBag.ProjectTasks = await _context.Tasks
                    .Include(t => t.Project)
                    .Include(t => t.AssignedTo)
                    .Where(t => t.Project.ClientId == currentUser.Id)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                return View("Client");
            }
        }
    }
} 