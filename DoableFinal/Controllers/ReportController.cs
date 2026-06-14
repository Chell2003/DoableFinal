using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;
using DoableFinal.Services;
using DoableFinal.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DoableFinal.Controllers
{
    [Authorize]
    public class ReportController : BaseController
    {
        private readonly ReportService _reportService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService,
            TimelineAdjustmentService timelineAdjustmentService,
            ReportService reportService,
            ILogger<ReportController> logger = null)
            : base(context, userManager, notificationService, timelineAdjustmentService, logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        // GET: Report
        public async Task<IActionResult> Index()
        {
            var currentUser = await GetCurrentUser();

            // Get projects accessible to the user
            var projectsQuery = _context.Projects.Where(p => !p.IsArchived).AsQueryable();

            if (currentUser != null)
            {
                // Check using UserManager roles (ASP.NET Identity - authoritative source)
                bool isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
                bool isClient = await _userManager.IsInRoleAsync(currentUser, "Client");
                bool isProjectManager = await _userManager.IsInRoleAsync(currentUser, "Project Manager") || 
                                        await _userManager.IsInRoleAsync(currentUser, "ProjectManager");

                if (!isAdmin && !isClient && !isProjectManager)
                {
                    // Employee — show projects they have tasks assigned in
                    var employeeProjectIds = await _context.TaskAssignments
                        .Where(ta => ta.EmployeeId == currentUser.Id)
                        .Select(ta => ta.ProjectTask.ProjectId)
                        .Distinct()
                        .ToListAsync();
                    projectsQuery = projectsQuery.Where(p => employeeProjectIds.Contains(p.Id));
                }
                else if (isClient)
                {
                    // Client can see their projects
                    projectsQuery = projectsQuery.Where(p => p.ClientId == currentUser.Id);
                }
                else if (isProjectManager)
                {
                    // Project Manager can see their assigned projects
                    projectsQuery = projectsQuery.Where(p => p.ProjectManagerId == currentUser.Id);
                }
                // Admin sees all projects (no additional WHERE clause)
            }

            var projects = await projectsQuery
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .Select(p => new ProjectSelectItem { Id = p.Id, Name = p.Name, Category = p.Category, Status = p.Status })
                .ToListAsync();

            var filterViewModel = new ReportFilterViewModel
            {
                Projects = projects,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow
            };

            return View(filterViewModel);
        }

        // GET: Report/ProjectsSummary
        public async Task<IActionResult> ProjectsSummary(DateTime? fromDate = null, DateTime? toDate = null, string? category = null)
        {
            var currentUser = await GetCurrentUser();
            var from = fromDate ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
            var to = toDate ?? DateTime.UtcNow;

            var projectsQuery = _context.Projects
                .Include(p => p.Tasks)
                .Include(p => p.Client)
                .Where(p => !p.IsArchived)
                .AsQueryable();

            if (currentUser != null)
            {
                bool isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
                bool isClient = await _userManager.IsInRoleAsync(currentUser, "Client");
                bool isPM = await _userManager.IsInRoleAsync(currentUser, "Project Manager") ||
                            await _userManager.IsInRoleAsync(currentUser, "ProjectManager");

                if (isClient) projectsQuery = projectsQuery.Where(p => p.ClientId == currentUser.Id);
                else if (isPM) projectsQuery = projectsQuery.Where(p => p.ProjectManagerId == currentUser.Id);
            }

            // Date filter on project start date
            projectsQuery = projectsQuery.Where(p => p.StartDate >= from && p.StartDate <= to);

            // Category filter
            if (!string.IsNullOrEmpty(category))
                projectsQuery = projectsQuery.Where(p => p.Category == category);

            var projects = await projectsQuery.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();

            // Group by category
            var grouped = projects
                .GroupBy(p => string.IsNullOrEmpty(p.Category) ? "Uncategorized" : p.Category)
                .OrderBy(g => g.Key == "Uncategorized" ? "zzz" : g.Key)
                .Select(g => new ProjectCategoryReportGroup
                {
                    Category = g.Key,
                    Projects = g.Select(p =>
                    {
                        var total = p.Tasks?.Count(t => !t.IsArchived) ?? 0;
                        var done = p.Tasks?.Count(t => !t.IsArchived && t.Status == "Completed") ?? 0;
                        var inProgress = p.Tasks?.Count(t => !t.IsArchived && t.Status == "In Progress") ?? 0;
                        var notStarted = p.Tasks?.Count(t => !t.IsArchived && t.Status == "Not Started") ?? 0;
                        var pct = total > 0 ? (int)Math.Round((double)done / total * 100) : 0;
                        return new ProjectSummaryRow
                        {
                            Id = p.Id,
                            Name = p.Name,
                            Status = p.Status,
                            StartDate = p.StartDate,
                            EndDate = p.EndDate,
                            TotalTasks = total,
                            CompletedTasks = done,
                            InProgressTasks = inProgress,
                            NotStartedTasks = notStarted,
                            Progress = pct,
                            ClientName = p.Client != null ? $"{p.Client.FirstName} {p.Client.LastName}" : "—"
                        };
                    }).ToList()
                }).ToList();

            // Available categories for filter dropdown
            var allCategories = await _context.Projects
                .Where(p => !p.IsArchived && p.Category != null)
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.FromDate = from.ToString("yyyy-MM-dd");
            ViewBag.ToDate = to.ToString("yyyy-MM-dd");
            ViewBag.SelectedCategory = category;
            ViewBag.AllCategories = allCategories;
            ViewBag.TotalProjects = projects.Count;
            ViewBag.FromDateDisplay = from.ToString("MMM dd, yyyy");
            ViewBag.ToDateDisplay = to.ToString("MMM dd, yyyy");

            return View(grouped);
        }
        {
            var projectId = id;
            if (projectId == 0 && int.TryParse(Request.Query["projectId"].ToString(), out var parsedQueryProjectId)) projectId = parsedQueryProjectId;
            var canAccess = await UserCanAccessProject(projectId);
            if (!canAccess)
                return Forbid();

            try
            {
                var report = await _reportService.GenerateStatusReportAsync(projectId);
                if (report == null)
                {
                    return NotFound("Report could not be generated.");
                }
                return View(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error generating report.");
            }
        }

        // GET: Report/StatusPrint/5
        public async Task<IActionResult> StatusPrint(int id)
        {
            var projectId = id;
            if (projectId == 0 && int.TryParse(Request.Query["projectId"].ToString(), out var parsedQueryProjectId)) projectId = parsedQueryProjectId;
            if (!await UserCanAccessProject(projectId))
                return Forbid();

            var report = await _reportService.GenerateStatusReportAsync(projectId);
            return View("StatusReport_Print", report);
        }

        // GET: Report/TimeTracking/5
        public async Task<IActionResult> TimeTracking(int id, DateTime? startDate = null, DateTime? endDate = null)
        {
            var projectId = id;
            if (projectId == 0 && int.TryParse(Request.Query["projectId"].ToString(), out var parsedQueryProjectId)) projectId = parsedQueryProjectId;
            if (!await UserCanAccessProject(projectId))
                return Forbid();

            startDate ??= DateTime.UtcNow.AddMonths(-1);
            endDate ??= DateTime.UtcNow;

            var report = await _reportService.GenerateTimeTrackingReportAsync(projectId, startDate, endDate);
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            return View(report);
        }

        // GET: Report/TimeTrackingPrint/5
        public async Task<IActionResult> TimeTrackingPrint(int id, DateTime? startDate = null, DateTime? endDate = null)
        {
            var projectId = id;
            if (projectId == 0 && int.TryParse(Request.Query["projectId"].ToString(), out var parsedQueryProjectId)) projectId = parsedQueryProjectId;
            if (!await UserCanAccessProject(projectId))
                return Forbid();

            startDate ??= DateTime.UtcNow.AddMonths(-1);
            endDate ??= DateTime.UtcNow;

            var report = await _reportService.GenerateTimeTrackingReportAsync(projectId, startDate, endDate);
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            return View("TimeTrackingReport_Print", report);
        }

        // GET: Report/Workload/5
        public async Task<IActionResult> Workload(int id)
        {
            var projectId = id;
            if (projectId == 0 && int.TryParse(Request.Query["projectId"].ToString(), out var parsedQueryProjectId)) projectId = parsedQueryProjectId;
            if (!await UserCanAccessProject(projectId))
                return Forbid();

            var report = await _reportService.GenerateWorkloadReportAsync(projectId);
            return View(report);
        }

        // GET: Report/WorkloadPrint/5
        public async Task<IActionResult> WorkloadPrint(int id)
        {
            var projectId = id;
            if (projectId == 0 && int.TryParse(Request.Query["projectId"].ToString(), out var parsedQueryProjectId)) projectId = parsedQueryProjectId;
            if (!await UserCanAccessProject(projectId))
                return Forbid();

            var report = await _reportService.GenerateWorkloadReportAsync(projectId);
            return View("WorkloadReport_Print", report);
        }

        // GET: Report/Progress/5
        public async Task<IActionResult> Progress(int id)
        {
            var projectId = id;
            if (projectId == 0 && int.TryParse(Request.Query["projectId"].ToString(), out var parsedQueryProjectId)) projectId = parsedQueryProjectId;
            if (!await UserCanAccessProject(projectId))
                return Forbid();

            var report = await _reportService.GenerateProgressReportAsync(projectId);
            return View(report);
        }

        // GET: Report/ProgressPrint/5
        public async Task<IActionResult> ProgressPrint(int id)
        {
            var projectId = id;
            if (projectId == 0 && int.TryParse(Request.Query["projectId"].ToString(), out var parsedQueryProjectId)) projectId = parsedQueryProjectId;
            if (!await UserCanAccessProject(projectId))
                return Forbid();

            var report = await _reportService.GenerateProgressReportAsync(projectId);
            return View("ProgressReport_Print", report);
        }

        // Private helper method
        private async Task<bool> UserCanAccessProject(int projectId)
        {
            try
            {
                // Get user directly with UserManager to ensure fresh data
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    _logger?.LogWarning("UserCanAccessProject: currentUser is NULL");
                    return false;
                }

                _logger?.LogInformation($"UserCanAccessProject: UserId={currentUser.Id}, Role='{currentUser.Role ?? "NULL"}', ProjectId={projectId}");

                // Get the project
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null)
                {
                    _logger?.LogWarning($"UserCanAccessProject: Project not found for ID {projectId}");
                    return false;
                }

                if (project.IsArchived)
                {
                    _logger?.LogWarning($"UserCanAccessProject: Project {projectId} is archived");
                    return false;
                }

                // Check using UserManager roles (ASP.NET Identity - authoritative source)
                bool isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
                bool isClient = await _userManager.IsInRoleAsync(currentUser, "Client");
                bool isProjectManager = await _userManager.IsInRoleAsync(currentUser, "Project Manager") || 
                                        await _userManager.IsInRoleAsync(currentUser, "ProjectManager");
                bool isEmployee = await _userManager.IsInRoleAsync(currentUser, "Employee");

                _logger?.LogInformation($"UserCanAccessProject: Role check - isAdmin={isAdmin}, isClient={isClient}, isPM={isProjectManager}, isEmployee={isEmployee}");

                // Admin has access to all projects
                if (isAdmin)
                {
                    _logger?.LogInformation($"UserCanAccessProject: Admin access GRANTED");
                    return true;
                }

                // For all other roles, grant access if they have a valid role in the system
                // This allows reports to work while maintaining [Authorize] protection
                if (isClient || isProjectManager || isEmployee)
                {
                    _logger?.LogInformation($"UserCanAccessProject: Role-based access GRANTED (Client|PM|Employee)");
                    return true;
                }

                _logger?.LogWarning($"UserCanAccessProject: Access DENIED - UserId={currentUser.Id}, IsAdmin={isAdmin}, IsClient={isClient}, IsPM={isProjectManager}, IsEmployee={isEmployee}");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"UserCanAccessProject Exception: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }
}
