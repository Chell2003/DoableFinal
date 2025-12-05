using System;
using System.Collections.Generic;

namespace DoableFinal.ViewModels
{
    public class StatusReportViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        public List<TaskStatusItem> CompletedTasks { get; set; } = new();
        public List<TaskStatusItem> InProgressTasks { get; set; } = new();
        public List<TaskStatusItem> UpcomingTasks { get; set; } = new();
        public int TotalTasks { get; set; }
        public decimal CompletionPercentage { get; set; }
    }

    public class TaskStatusItem
    {
        public int TaskId { get; set; }
        public string Title { get; set; }
        public string Priority { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string AssignedTo { get; set; }
        public string Status { get; set; }
    }

    public class TimeTrackingReportViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<TaskTimeItem> TaskTimes { get; set; } = new();
        public List<EmployeeTimeItem> EmployeeTimes { get; set; } = new();
        public List<DailyTimeItem> DailyBreakdown { get; set; } = new();
        public List<WeeklyTimeItem> WeeklyBreakdown { get; set; } = new();
        public decimal TotalHours { get; set; }
    }

    public class TaskTimeItem
    {
        public int TaskId { get; set; }
        public string TaskTitle { get; set; }
        public decimal Hours { get; set; }
        public string Status { get; set; }
    }

    public class EmployeeTimeItem
    {
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public decimal TotalHours { get; set; }
        public int TaskCount { get; set; }
        public decimal AverageHoursPerTask { get; set; }
    }

    public class DailyTimeItem
    {
        public DateTime Date { get; set; }
        public decimal Hours { get; set; }
        public int TaskCount { get; set; }
    }

    public class WeeklyTimeItem
    {
        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }
        public decimal Hours { get; set; }
        public int TaskCount { get; set; }
    }

    public class WorkloadReportViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        public List<EmployeeWorkloadItem> EmployeeWorkloads { get; set; } = new();
        public List<OverallocationAlert> OverallocationAlerts { get; set; } = new();
        public decimal AverageTasksPerEmployee { get; set; }
        public string MostLoadedEmployee { get; set; }
        public string LeastLoadedEmployee { get; set; }
    }

    public class EmployeeWorkloadItem
    {
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public int AssignedTaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public int InProgressTaskCount { get; set; }
        public int OverdueTaskCount { get; set; }
        public decimal WorkloadPercentage { get; set; }
        public List<AssignedTaskDetail> Tasks { get; set; } = new();
    }

    public class AssignedTaskDetail
    {
        public int TaskId { get; set; }
        public string TaskTitle { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public DateTime DueDate { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class OverallocationAlert
    {
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public int TaskCount { get; set; }
        public int OverdueTaskCount { get; set; }
        public string SeverityLevel { get; set; } // Low, Medium, High
        public string Recommendation { get; set; }
    }

    public class ProgressReportViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        public decimal CompletionPercentage { get; set; }
        public DateTime ProjectStartDate { get; set; }
        public DateTime? ProjectEndDate { get; set; }
        public DateTime? ProjectExpectedEndDate { get; set; }
        public string ProjectStatus { get; set; }
        public List<MilestoneItem> Milestones { get; set; } = new();
        public TaskBreakdownItem TaskBreakdown { get; set; } = new();
        public ProjectHealthIndicator HealthIndicator { get; set; } = new();
    }

    public class MilestoneItem
    {
        public int MilestoneIndex { get; set; }
        public string Title { get; set; }
        public DateTime TargetDate { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Status { get; set; } // Completed, On Track, At Risk, Delayed
        public string Description { get; set; }
    }

    public class TaskBreakdownItem
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int NotStartedTasks { get; set; }
        public int OverdueTasks { get; set; }
    }

    public class ProjectHealthIndicator
    {
        public string OverallHealth { get; set; } // Green, Yellow, Red
        public string ScheduleHealth { get; set; } // On Track, At Risk, Delayed
        public string ResourceHealth { get; set; } // Adequate, Overloaded, Underutilized
        public string QualityHealth { get; set; } // Good, Fair, Poor
        public List<string> Risks { get; set; } = new();
        public List<string> Achievements { get; set; } = new();
    }

    public class ReportFilterViewModel
    {
        public int? ProjectId { get; set; }
        public string? ReportType { get; set; } // Status, TimeTracking, Workload, Progress
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IncludeArchivedTasks { get; set; } = false;
        public List<ProjectSelectItem> Projects { get; set; } = new();
    }

    public class ProjectSelectItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
