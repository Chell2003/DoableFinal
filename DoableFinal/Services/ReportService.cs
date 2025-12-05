using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DoableFinal.Services
{
    public class ReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===== STATUS REPORT =====
        public async Task<StatusReportViewModel> GenerateStatusReportAsync(int projectId)
        {
            var project = await _context.Projects
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return new StatusReportViewModel();

            var tasks = project.Tasks.Where(t => !t.IsArchived).ToList();

            var completedTasks = tasks
                .Where(t => t.Status == "Completed")
                .Select(t => MapToTaskStatusItem(t))
                .ToList();

            var inProgressTasks = tasks
                .Where(t => t.Status == "In Progress")
                .Select(t => MapToTaskStatusItem(t))
                .ToList();

            var upcomingTasks = tasks
                .Where(t => t.Status == "Not Started")
                .Select(t => MapToTaskStatusItem(t))
                .ToList();

            var totalTasks = tasks.Count;
            var completionPercentage = totalTasks > 0 
                ? (completedTasks.Count * 100m) / totalTasks 
                : 0;

            return new StatusReportViewModel
            {
                ProjectId = projectId,
                ProjectName = project.Name,
                GeneratedDate = DateTime.UtcNow,
                CompletedTasks = completedTasks,
                InProgressTasks = inProgressTasks,
                UpcomingTasks = upcomingTasks,
                TotalTasks = totalTasks,
                CompletionPercentage = completionPercentage
            };
        }

        // ===== TIME TRACKING REPORT =====
        public async Task<TimeTrackingReportViewModel> GenerateTimeTrackingReportAsync(
            int projectId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var project = await _context.Projects
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return new TimeTrackingReportViewModel();

            var tasks = project.Tasks.Where(t => !t.IsArchived).ToList();

            // Get time entries within date range
            var query = _context.TimeEntries
                .Include(te => te.ProjectTask)
                .Include(te => te.Employee)
                .Where(te => tasks.Select(t => t.Id).Contains(te.ProjectTaskId));

            if (startDate.HasValue)
                query = query.Where(te => te.StartTime.Date >= startDate.Value.Date);
            if (endDate.HasValue)
                query = query.Where(te => te.EndTime.Date <= endDate.Value.Date);

            var timeEntries = await query.ToListAsync();

            // Calculate task times
            var taskTimes = tasks.Select(t =>
            {
                var entries = timeEntries.Where(te => te.ProjectTaskId == t.Id).ToList();
                var totalHours = entries.Sum(e => (decimal)(e.EndTime - e.StartTime).TotalHours);
                return new TaskTimeItem
                {
                    TaskId = t.Id,
                    TaskTitle = t.Title,
                    Hours = totalHours,
                    Status = t.Status
                };
            }).Where(x => x.Hours > 0).ToList();

            // Calculate employee times
            var employeeTimes = new Dictionary<string, EmployeeTimeItem>();
            foreach (var entry in timeEntries)
            {
                var key = entry.EmployeeId;
                var hours = (decimal)(entry.EndTime - entry.StartTime).TotalHours;

                if (!employeeTimes.ContainsKey(key))
                {
                    employeeTimes[key] = new EmployeeTimeItem
                    {
                        EmployeeId = entry.EmployeeId,
                        EmployeeName = $"{entry.Employee.FirstName} {entry.Employee.LastName}",
                        TotalHours = 0,
                        TaskCount = 0
                    };
                }

                employeeTimes[key].TotalHours += hours;
                employeeTimes[key].TaskCount = timeEntries
                    .Where(te => te.EmployeeId == key)
                    .Select(te => te.ProjectTaskId)
                    .Distinct()
                    .Count();
            }

            var employeeTimesList = employeeTimes.Values.ToList();
            foreach (var emp in employeeTimesList)
            {
                emp.AverageHoursPerTask = emp.TaskCount > 0 ? emp.TotalHours / emp.TaskCount : 0;
            }

            // Calculate daily breakdown
            var dailyBreakdown = timeEntries
                .GroupBy(te => te.StartTime.Date)
                .Select(g => new DailyTimeItem
                {
                    Date = g.Key,
                    Hours = (decimal)g.Sum(te => (te.EndTime - te.StartTime).TotalHours),
                    TaskCount = g.Select(te => te.ProjectTaskId).Distinct().Count()
                })
                .OrderBy(d => d.Date)
                .ToList();

            // Calculate weekly breakdown
            var weeklyBreakdown = timeEntries
                .GroupBy(te => GetWeekStart(te.StartTime))
                .Select(g => new WeeklyTimeItem
                {
                    WeekStartDate = g.Key,
                    WeekEndDate = g.Key.AddDays(6),
                    Hours = (decimal)g.Sum(te => (te.EndTime - te.StartTime).TotalHours),
                    TaskCount = g.Select(te => te.ProjectTaskId).Distinct().Count()
                })
                .OrderBy(w => w.WeekStartDate)
                .ToList();

            var totalHours = taskTimes.Sum(t => t.Hours);

            return new TimeTrackingReportViewModel
            {
                ProjectId = projectId,
                ProjectName = project.Name,
                GeneratedDate = DateTime.UtcNow,
                StartDate = startDate,
                EndDate = endDate,
                TaskTimes = taskTimes.OrderByDescending(t => t.Hours).ToList(),
                EmployeeTimes = employeeTimesList.OrderByDescending(e => e.TotalHours).ToList(),
                DailyBreakdown = dailyBreakdown,
                WeeklyBreakdown = weeklyBreakdown,
                TotalHours = totalHours
            };
        }

        // ===== WORKLOAD REPORT =====
        public async Task<WorkloadReportViewModel> GenerateWorkloadReportAsync(int projectId)
        {
            var project = await _context.Projects
                .Include(p => p.Tasks)
                .ThenInclude(t => t.TaskAssignments)
                .ThenInclude(ta => ta.Employee)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return new WorkloadReportViewModel();

            var tasks = project.Tasks.Where(t => !t.IsArchived).ToList();
            var employeeWorkloads = new Dictionary<string, EmployeeWorkloadItem>();

            // Group tasks by employee
            foreach (var task in tasks)
            {
                foreach (var assignment in task.TaskAssignments)
                {
                    var key = assignment.EmployeeId;
                    if (!employeeWorkloads.ContainsKey(key))
                    {
                        employeeWorkloads[key] = new EmployeeWorkloadItem
                        {
                            EmployeeId = assignment.EmployeeId,
                            EmployeeName = $"{assignment.Employee.FirstName} {assignment.Employee.LastName}",
                            AssignedTaskCount = 0,
                            CompletedTaskCount = 0,
                            InProgressTaskCount = 0,
                            OverdueTaskCount = 0,
                            Tasks = new()
                        };
                    }

                    employeeWorkloads[key].AssignedTaskCount++;

                    if (task.Status == "Completed")
                        employeeWorkloads[key].CompletedTaskCount++;
                    else if (task.Status == "In Progress")
                        employeeWorkloads[key].InProgressTaskCount++;

                    if (task.Status != "Completed" && task.DueDate < DateTime.UtcNow)
                        employeeWorkloads[key].OverdueTaskCount++;

                    employeeWorkloads[key].Tasks.Add(new AssignedTaskDetail
                    {
                        TaskId = task.Id,
                        TaskTitle = task.Title,
                        Status = task.Status,
                        Priority = task.Priority,
                        DueDate = task.DueDate,
                        IsOverdue = task.Status != "Completed" && task.DueDate < DateTime.UtcNow
                    });
                }
            }

            var employeeList = employeeWorkloads.Values.ToList();
            var averageTasksPerEmployee = employeeList.Count > 0 
                ? (decimal)employeeList.Average(e => e.AssignedTaskCount) 
                : 0;

            // Calculate workload percentage
            var maxTasks = employeeList.Count > 0 ? employeeList.Max(e => e.AssignedTaskCount) : 1;
            foreach (var emp in employeeList)
            {
                emp.WorkloadPercentage = (emp.AssignedTaskCount * 100m) / maxTasks;
            }

            // Identify overallocation alerts
            var overallocationAlerts = new List<OverallocationAlert>();
            foreach (var emp in employeeList)
            {
                if (emp.OverdueTaskCount > 0 || emp.InProgressTaskCount > 5)
                {
                    var severity = emp.OverdueTaskCount > 3 || emp.InProgressTaskCount > 8 ? "High" : "Medium";
                    var recommendation = emp.OverdueTaskCount > 0 
                        ? $"Address {emp.OverdueTaskCount} overdue tasks urgently"
                        : $"Consider redistributing some of {emp.EmployeeName}'s tasks";

                    overallocationAlerts.Add(new OverallocationAlert
                    {
                        EmployeeId = emp.EmployeeId,
                        EmployeeName = emp.EmployeeName,
                        TaskCount = emp.AssignedTaskCount,
                        OverdueTaskCount = emp.OverdueTaskCount,
                        SeverityLevel = severity,
                        Recommendation = recommendation
                    });
                }
            }

            var mostLoadedEmployee = employeeList.OrderByDescending(e => e.AssignedTaskCount).FirstOrDefault()?.EmployeeName ?? "N/A";
            var leastLoadedEmployee = employeeList.OrderBy(e => e.AssignedTaskCount).FirstOrDefault()?.EmployeeName ?? "N/A";

            return new WorkloadReportViewModel
            {
                ProjectId = projectId,
                ProjectName = project.Name,
                GeneratedDate = DateTime.UtcNow,
                EmployeeWorkloads = employeeList.OrderByDescending(e => e.AssignedTaskCount).ToList(),
                OverallocationAlerts = overallocationAlerts,
                AverageTasksPerEmployee = averageTasksPerEmployee,
                MostLoadedEmployee = mostLoadedEmployee,
                LeastLoadedEmployee = leastLoadedEmployee
            };
        }

        // ===== PROGRESS REPORT =====
        public async Task<ProgressReportViewModel> GenerateProgressReportAsync(int projectId)
        {
            var project = await _context.Projects
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return new ProgressReportViewModel();

            var tasks = project.Tasks.Where(t => !t.IsArchived).ToList();
            var totalTasks = tasks.Count;
            var completedTasks = tasks.Count(t => t.Status == "Completed");
            var inProgressTasks = tasks.Count(t => t.Status == "In Progress");
            var notStartedTasks = tasks.Count(t => t.Status == "Not Started");
            var overdueTasks = tasks.Count(t => t.Status != "Completed" && t.DueDate < DateTime.UtcNow);

            var completionPercentage = totalTasks > 0 ? (completedTasks * 100m) / totalTasks : 0;

            // Generate milestones based on task distribution
            var milestones = GenerateMilestones(tasks);

            // Determine health indicators
            var health = CalculateProjectHealth(tasks, project, overdueTasks, inProgressTasks);

            return new ProgressReportViewModel
            {
                ProjectId = projectId,
                ProjectName = project.Name,
                GeneratedDate = DateTime.UtcNow,
                CompletionPercentage = completionPercentage,
                ProjectStartDate = project.StartDate,
                ProjectEndDate = project.EndDate,
                ProjectExpectedEndDate = EstimateEndDate(tasks, project),
                ProjectStatus = project.Status,
                Milestones = milestones,
                TaskBreakdown = new TaskBreakdownItem
                {
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    InProgressTasks = inProgressTasks,
                    NotStartedTasks = notStartedTasks,
                    OverdueTasks = overdueTasks
                },
                HealthIndicator = health
            };
        }

        // ===== HELPER METHODS =====
        private TaskStatusItem MapToTaskStatusItem(ProjectTask task)
        {
            return new TaskStatusItem
            {
                TaskId = task.Id,
                Title = task.Title,
                Priority = task.Priority,
                DueDate = task.DueDate,
                CompletedAt = task.CompletedAt,
                AssignedTo = "Assigned", // Would need to load assignments
                Status = task.Status
            };
        }

        private DateTime GetWeekStart(DateTime date)
        {
            var diff = date.DayOfWeek - DayOfWeek.Monday;
            if (diff < 0) diff += 7;
            return date.AddDays(-diff).Date;
        }

        private List<MilestoneItem> GenerateMilestones(List<ProjectTask> tasks)
        {
            var milestones = new List<MilestoneItem>();
            var sortedTasks = tasks.OrderBy(t => t.DueDate).ToList();

            // Create milestones every 25% of tasks
            var milestoneInterval = Math.Max(1, (int)Math.Ceiling(sortedTasks.Count / 4.0));

            for (int i = 0; i < sortedTasks.Count; i += milestoneInterval)
            {
                var taskGroup = sortedTasks.Skip(i).Take(milestoneInterval).ToList();
                if (taskGroup.Count == 0) continue;

                var milestone = new MilestoneItem
                {
                    MilestoneIndex = (i / milestoneInterval) + 1,
                    Title = $"Phase {(i / milestoneInterval) + 1} - {taskGroup.First().Title}",
                    TargetDate = taskGroup.Last().DueDate,
                    IsCompleted = taskGroup.All(t => t.Status == "Completed"),
                    CompletedDate = taskGroup.Any(t => t.CompletedAt.HasValue) 
                        ? taskGroup.Where(t => t.CompletedAt.HasValue).Max(t => t.CompletedAt)
                        : null,
                    Status = DetermineMilestoneStatus(taskGroup),
                    Description = $"{taskGroup.Count} tasks"
                };
                milestones.Add(milestone);
            }

            return milestones;
        }

        private string DetermineMilestoneStatus(List<ProjectTask> tasks)
        {
            var allCompleted = tasks.All(t => t.Status == "Completed");
            if (allCompleted) return "Completed";

            var allOverdue = tasks.All(t => t.DueDate < DateTime.UtcNow && t.Status != "Completed");
            if (allOverdue) return "Delayed";

            var someOverdue = tasks.Any(t => t.DueDate < DateTime.UtcNow && t.Status != "Completed");
            if (someOverdue) return "At Risk";

            return "On Track";
        }

        private DateTime? EstimateEndDate(List<ProjectTask> tasks, Project project)
        {
            if (tasks.Count == 0) return project.EndDate;

            var lastTaskDate = tasks.Max(t => t.DueDate);
            return lastTaskDate > (project.EndDate ?? DateTime.MaxValue) ? lastTaskDate : project.EndDate;
        }

        private ProjectHealthIndicator CalculateProjectHealth(
            List<ProjectTask> tasks, Project project, int overdueTasks, int inProgressTasks)
        {
            var totalTasks = tasks.Count;
            var completedTasks = tasks.Count(t => t.Status == "Completed");
            var completionPercentage = totalTasks > 0 ? (completedTasks * 100.0) / totalTasks : 0;

            var scheduleHealth = overdueTasks > totalTasks * 0.2 ? "Delayed" : 
                                 overdueTasks > totalTasks * 0.1 ? "At Risk" : 
                                 "On Track";

            var resourceHealth = inProgressTasks > totalTasks * 0.7 ? "Overloaded" :
                                inProgressTasks < totalTasks * 0.2 ? "Underutilized" :
                                "Adequate";

            var qualityHealth = overdueTasks == 0 ? "Good" :
                               overdueTasks <= totalTasks * 0.1 ? "Fair" :
                               "Poor";

            var overallHealth = (scheduleHealth == "On Track" && resourceHealth == "Adequate" && qualityHealth == "Good") 
                ? "Green" 
                : (scheduleHealth == "Delayed" || resourceHealth == "Overloaded" || qualityHealth == "Poor")
                ? "Red"
                : "Yellow";

            var risks = new List<string>();
            var achievements = new List<string>();

            if (overdueTasks > 0)
                risks.Add($"{overdueTasks} overdue tasks detected");
            if (completionPercentage < 25)
                risks.Add("Project completion significantly behind schedule");

            if (completionPercentage > 75)
                achievements.Add("Project is 75%+ complete");
            if (overdueTasks == 0 && completionPercentage > 50)
                achievements.Add("No overdue tasks - good progress!");

            return new ProjectHealthIndicator
            {
                OverallHealth = overallHealth,
                ScheduleHealth = scheduleHealth,
                ResourceHealth = resourceHealth,
                QualityHealth = qualityHealth,
                Risks = risks,
                Achievements = achievements
            };
        }
    }
}
