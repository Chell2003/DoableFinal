using System;
using System.Collections.Generic;

namespace DoableFinal.ViewModels
{
    public class StatusReportViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string? ProjectCategory { get; set; }
        public string? ProjectStatus { get; set; }
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
        public string? Category { get; set; }
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
        public string? Category { get; set; }
        public string? Status { get; set; }
    }

    public class ProjectCategoryReportGroup
    {
        public string Category { get; set; }
        public List<ProjectSummaryRow> Projects { get; set; } = new();
        public int TotalProjects => Projects.Count;
        public int TotalTasks => Projects.Sum(p => p.TotalTasks);
        public int CompletedTasks => Projects.Sum(p => p.CompletedTasks);
        public int InProgressTasks => Projects.Sum(p => p.InProgressTasks);
        public int NotStartedTasks => Projects.Sum(p => p.NotStartedTasks);
        public int AvgProgress => Projects.Count > 0 ? (int)Math.Round(Projects.Average(p => (double)p.Progress)) : 0;
    }

    public class ProjectSummaryRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int NotStartedTasks { get; set; }
        public int Progress { get; set; }
        public string ClientName { get; set; }
    }
}

    // ── Cross-project / Overview report models ────────────────────────

    public class CategoryReportViewModel
    {
        public string Category { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
        public List<CategoryProjectSummary> Projects { get; set; } = new();
        public int TotalProjects  { get; set; }
        public int TotalTasks     { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks   { get; set; }
        public int InProgressTasks{ get; set; }
        public decimal OverallCompletion { get; set; }
    }

    public class CategoryProjectSummary
    {
        public int    ProjectId          { get; set; }
        public string ProjectName        { get; set; }
        public string Status             { get; set; }
        public string ProjectManager     { get; set; }
        public int    TotalTasks         { get; set; }
        public int    CompletedTasks     { get; set; }
        public int    InProgressTasks    { get; set; }
        public int    OverdueTasks       { get; set; }
        public decimal CompletionPct     { get; set; }
        public DateTime? EndDate         { get; set; }
    }

    public class AllProjectsReportViewModel
    {
        public DateTime GeneratedDate    { get; set; } = DateTime.UtcNow;
        public string? FilterStatus      { get; set; }
        public DateTime? FromDate        { get; set; }
        public DateTime? ToDate          { get; set; }
        public int TotalProjects         { get; set; }
        public int TotalTasks            { get; set; }
        public int CompletedTasks        { get; set; }
        public int OverdueTasks          { get; set; }
        public int ActiveProjects        { get; set; }
        public decimal OverallCompletion { get; set; }
        public List<AllProjectRow> Projects { get; set; } = new();
        public List<CategorySummaryRow> ByCategory { get; set; } = new();
    }

    public class AllProjectRow
    {
        public int    ProjectId       { get; set; }
        public string ProjectName     { get; set; }
        public string Category        { get; set; }
        public string Status          { get; set; }
        public string ProjectManager  { get; set; }
        public int    TotalTasks      { get; set; }
        public int    CompletedTasks  { get; set; }
        public int    OverdueTasks    { get; set; }
        public decimal CompletionPct  { get; set; }
        public DateTime? EndDate      { get; set; }
        public DateTime  StartDate    { get; set; }
    }

    public class CategorySummaryRow
    {
        public string Category        { get; set; }
        public int    ProjectCount    { get; set; }
        public int    TotalTasks      { get; set; }
        public int    CompletedTasks  { get; set; }
        public int    OverdueTasks    { get; set; }
        public decimal CompletionPct  { get; set; }
    }

    public class TeamPerformanceReportViewModel
    {
        public DateTime GeneratedDate     { get; set; } = DateTime.UtcNow;
        public DateTime? FromDate         { get; set; }
        public DateTime? ToDate           { get; set; }
        public int TotalEmployees         { get; set; }
        public int TotalTasksAssigned     { get; set; }
        public int TotalTasksCompleted    { get; set; }
        public int TotalOverdue           { get; set; }
        public List<EmployeePerformanceRow> Employees { get; set; } = new();
    }

    public class EmployeePerformanceRow
    {
        public string EmployeeId         { get; set; }
        public string EmployeeName       { get; set; }
        public string Position           { get; set; }
        public int    TotalAssigned      { get; set; }
        public int    Completed          { get; set; }
        public int    InProgress         { get; set; }
        public int    Overdue            { get; set; }
        public decimal CompletionRate    { get; set; }
        public List<string> Categories  { get; set; } = new();
    }
