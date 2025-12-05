# Printable Reports Feature - File Reference Guide

## 📂 Complete File Inventory

### New Models
```
DoableFinal/Models/TimeEntry.cs
- Namespace: DoableFinal.Models
- Key Properties: Id, ProjectTaskId, EmployeeId, StartTime, EndTime, Description, CreatedAt, UpdatedAt
- Related: ProjectTask, ApplicationUser
```

### New ViewModels
```
DoableFinal/ViewModels/ReportViewModels.cs
- Namespace: DoableFinal.ViewModels
- Classes:
  1. StatusReportViewModel
     - Properties: CompletedTasks, InProgressTasks, UpcomingTasks, CompletionPercentage
     - Nested: TaskStatusItem
  
  2. TimeTrackingReportViewModel
     - Properties: TaskTimes, EmployeeTimes, DailyBreakdown, WeeklyBreakdown, TotalHours
     - Nested: TaskTimeItem, EmployeeTimeItem, DailyTimeItem, WeeklyTimeItem
  
  3. WorkloadReportViewModel
     - Properties: EmployeeWorkloads, OverallocationAlerts, AverageTasksPerEmployee
     - Nested: EmployeeWorkloadItem, AssignedTaskDetail, OverallocationAlert
  
  4. ProgressReportViewModel
     - Properties: CompletionPercentage, Milestones, TaskBreakdown, HealthIndicator
     - Nested: MilestoneItem, TaskBreakdownItem, ProjectHealthIndicator
  
  5. ReportFilterViewModel
     - Properties: ProjectId, ReportType, StartDate, EndDate, Projects
     - Nested: ProjectSelectItem
```

### New Service
```
DoableFinal/Services/ReportService.cs
- Namespace: DoableFinal.Services
- Public Methods:
  • GenerateStatusReportAsync(int projectId) → StatusReportViewModel
  • GenerateTimeTrackingReportAsync(int projectId, DateTime?, DateTime?) → TimeTrackingReportViewModel
  • GenerateWorkloadReportAsync(int projectId) → WorkloadReportViewModel
  • GenerateProgressReportAsync(int projectId) → ProgressReportViewModel
- Private Helpers:
  • MapToTaskStatusItem()
  • GetWeekStart()
  • GenerateMilestones()
  • DetermineMilestoneStatus()
  • EstimateEndDate()
  • CalculateProjectHealth()
```

### New Controller
```
DoableFinal/Controllers/ReportController.cs
- Namespace: DoableFinal.Controllers
- Class: ReportController : BaseController
- Constructor: ReportService _reportService (injected)
- Public Actions:
  • Index() → View with ReportFilterViewModel
  • Status(int projectId) → StatusReportViewModel
  • StatusPrint(int projectId) → StatusReport_Print view
  • TimeTracking(int projectId, DateTime?, DateTime?) → TimeTrackingReportViewModel
  • TimeTrackingPrint(int projectId, DateTime?, DateTime?) → TimeTrackingReport_Print view
  • Workload(int projectId) → WorkloadReportViewModel
  • WorkloadPrint(int projectId) → WorkloadReport_Print view
  • Progress(int projectId) → ProgressReportViewModel
  • ProgressPrint(int projectId) → ProgressReport_Print view
- Private Methods:
  • UserCanAccessProject(int projectId) → bool (authorization)
```

### New Views - Interactive (Screen Display)
```
1. DoableFinal/Views/Report/Index.cshtml
   - Model: ReportFilterViewModel
   - Shows: Project selector, report type cards, date range modal
   - Features: Responsive grid, Bootstrap cards, JavaScript modal

2. DoableFinal/Views/Report/Status.cshtml
   - Model: StatusReportViewModel
   - Shows: Task breakdown, progress bar, completion status
   - Features: Summary cards, color-coded tables, print buttons

3. DoableFinal/Views/Report/TimeTracking.cshtml
   - Model: TimeTrackingReportViewModel
   - Shows: Task times, employee summaries, daily/weekly breakdown
   - Features: Data tables, hourly calculations, date range info

4. DoableFinal/Views/Report/Workload.cshtml
   - Model: WorkloadReportViewModel
   - Shows: Employee assignments, over-allocation alerts, workload %
   - Features: Alert boxes, detailed task lists, workload indicators

5. DoableFinal/Views/Report/Progress.cshtml
   - Model: ProgressReportViewModel
   - Shows: Health indicators, milestones, project timeline
   - Features: Status badges, milestone timeline, health dashboard
```

### New Views - Print Optimized (PDF-Ready)
```
1. DoableFinal/Views/Report/StatusReport_Print.cshtml
   - Layout: null (standalone HTML)
   - Styling: Print-specific CSS, page-break optimization
   - Format: A4 page with tables and sections

2. DoableFinal/Views/Report/TimeTrackingReport_Print.cshtml
   - Layout: null (standalone HTML)
   - Styling: Print-specific CSS, reduced font size
   - Format: Multi-page with daily/weekly breakdowns

3. DoableFinal/Views/Report/WorkloadReport_Print.cshtml
   - Layout: null (standalone HTML)
   - Styling: Print-specific CSS, alert boxes
   - Format: Employee sections with task lists

4. DoableFinal/Views/Report/ProgressReport_Print.cshtml
   - Layout: null (standalone HTML)
   - Styling: Print-specific CSS, health indicators
   - Format: Executive summary with milestones
```

### Modified Files

#### 1. DoableFinal/Program.cs
```csharp
// Added line in service registration section:
builder.Services.AddScoped<ReportService>();
```

#### 2. DoableFinal/Data/ApplicationDbContext.cs
```csharp
// Added property:
public DbSet<TimeEntry> TimeEntries { get; set; }
```

#### 3. DoableFinal/Views/Client/Projects.cshtml
```csharp
// Added button in Actions column (inside td):
<a asp-controller="Report" asp-action="Index" asp-route-projectId="@project.Id"
   class="btn btn-sm btn-outline-success" data-bs-toggle="tooltip" 
   data-bs-title="View project reports">
    <i class="bi bi-bar-chart me-1"></i> Reports
</a>
```

---

## 🔗 Dependency Injection Setup

### Program.cs Registration
```csharp
// In Program.cs, service registration section:
builder.Services.AddScoped<ReportService>();
```

### Constructor Injection (Controller)
```csharp
private readonly ReportService _reportService;

public ReportController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    NotificationService notificationService,
    TimelineAdjustmentService timelineAdjustmentService,
    ReportService reportService)  // <- Injected
    : base(context, userManager, notificationService, timelineAdjustmentService)
{
    _reportService = reportService;  // <- Assigned
}
```

---

## 📦 Using Reports in Your Code

### Import Statements Required
```csharp
using DoableFinal.Models;              // For TimeEntry
using DoableFinal.ViewModels;          // For Report ViewModels
using DoableFinal.Services;            // For ReportService
using DoableFinal.Controllers;         // For ReportController
using Microsoft.AspNetCore.Mvc;        // For MVC attributes
using Microsoft.AspNetCore.Authorization;  // For [Authorize]
```

### Generating Reports in Code
```csharp
// Inject ReportService where needed
private readonly ReportService _reportService;

// Usage Example
public async Task<IActionResult> MyAction()
{
    var statusReport = await _reportService.GenerateStatusReportAsync(projectId: 5);
    var timeReport = await _reportService.GenerateTimeTrackingReportAsync(
        projectId: 5, 
        startDate: DateTime.UtcNow.AddMonths(-1),
        endDate: DateTime.UtcNow
    );
    var workloadReport = await _reportService.GenerateWorkloadReportAsync(projectId: 5);
    var progressReport = await _reportService.GenerateProgressReportAsync(projectId: 5);
    
    return View(statusReport);
}
```

---

## 🌐 URL Routes

### Report Controller Routes
```
GET  /Report
GET  /Report/Status/{projectId}
GET  /Report/StatusPrint/{projectId}
GET  /Report/TimeTracking/{projectId}?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD
GET  /Report/TimeTrackingPrint/{projectId}?startDate=YYYY-MM-DD&endDate=YYYY-MM-DD
GET  /Report/Workload/{projectId}
GET  /Report/WorkloadPrint/{projectId}
GET  /Report/Progress/{projectId}
GET  /Report/ProgressPrint/{projectId}
```

### URL Helper Methods (Razor)
```html
@Url.Action("Index", "Report")
@Url.Action("Status", "Report", new { projectId = Model.ProjectId })
@Url.Action("StatusPrint", "Report", new { projectId = Model.ProjectId })
@Url.Action("TimeTracking", "Report", new { projectId = Model.ProjectId, startDate = "2025-01-01", endDate = "2025-12-31" })
```

---

## 🎯 Key Class Locations

### Models Location
```
File: DoableFinal/Models/TimeEntry.cs
Namespace: DoableFinal.Models
Public Class: TimeEntry
```

### ViewModels Location
```
File: DoableFinal/ViewModels/ReportViewModels.cs
Namespace: DoableFinal.ViewModels
Public Classes:
  - StatusReportViewModel
  - TimeTrackingReportViewModel
  - WorkloadReportViewModel
  - ProgressReportViewModel
  - ReportFilterViewModel
  - (20+ supporting classes)
```

### Service Location
```
File: DoableFinal/Services/ReportService.cs
Namespace: DoableFinal.Services
Public Class: ReportService
Constructor: ReportService(ApplicationDbContext context)
```

### Controller Location
```
File: DoableFinal/Controllers/ReportController.cs
Namespace: DoableFinal.Controllers
Public Class: ReportController : BaseController
Attributes: [Authorize]
```

### Views Location
```
Directory: DoableFinal/Views/Report/
Files:
  - Index.cshtml
  - Status.cshtml
  - StatusReport_Print.cshtml
  - TimeTracking.cshtml
  - TimeTrackingReport_Print.cshtml
  - Workload.cshtml
  - WorkloadReport_Print.cshtml
  - Progress.cshtml
  - ProgressReport_Print.cshtml
```

---

## 🔍 Key Properties Reference

### TimeEntry Model
```csharp
public class TimeEntry
{
    public int Id { get; set; }
    public int ProjectTaskId { get; set; }
    public ProjectTask ProjectTask { get; set; }  // Navigation
    
    public string EmployeeId { get; set; }
    public ApplicationUser Employee { get; set; }  // Navigation
    
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
```

### Report ViewModels - Key Properties
```csharp
// StatusReportViewModel
public decimal CompletionPercentage { get; set; }
public List<TaskStatusItem> CompletedTasks { get; set; }
public List<TaskStatusItem> InProgressTasks { get; set; }
public List<TaskStatusItem> UpcomingTasks { get; set; }

// TimeTrackingReportViewModel
public decimal TotalHours { get; set; }
public List<TaskTimeItem> TaskTimes { get; set; }
public List<EmployeeTimeItem> EmployeeTimes { get; set; }
public List<DailyTimeItem> DailyBreakdown { get; set; }
public List<WeeklyTimeItem> WeeklyBreakdown { get; set; }

// WorkloadReportViewModel
public List<EmployeeWorkloadItem> EmployeeWorkloads { get; set; }
public List<OverallocationAlert> OverallocationAlerts { get; set; }
public decimal AverageTasksPerEmployee { get; set; }

// ProgressReportViewModel
public decimal CompletionPercentage { get; set; }
public List<MilestoneItem> Milestones { get; set; }
public TaskBreakdownItem TaskBreakdown { get; set; }
public ProjectHealthIndicator HealthIndicator { get; set; }
```

---

## 📚 Documentation Files

```
1. PRINTABLE_REPORT_GUIDE.md
   - Complete feature documentation
   - Technical architecture
   - Calculations and algorithms
   - Customization guide
   
2. PRINTABLE_REPORT_IMPLEMENTATION.md
   - Implementation checklist
   - Testing scenarios
   - File inventory
   - Future enhancements

3. REPORTS_QUICK_REFERENCE.md
   - 5-minute quick start
   - Usage examples
   - Troubleshooting guide
   - Code snippets

4. REPORT_FEATURE_SUMMARY.md
   - Feature overview
   - Success criteria
   - Quality assurance
   - Change log
```

---

## 🚀 Quick Implementation Checklist

- [ ] Models created (TimeEntry)
- [ ] ViewModels created (4 types + helpers)
- [ ] Service created (ReportService)
- [ ] Controller created (ReportController)
- [ ] Views created (9 total)
- [ ] Service registered in Program.cs
- [ ] DbContext updated
- [ ] Navigation added to Projects view
- [ ] Database migration created
- [ ] Database updated with migration
- [ ] Test all report types
- [ ] Verify authorization works
- [ ] Test print functionality

---

**Reference Guide Version:** 1.0  
**Last Updated:** December 5, 2025  
**Status:** Complete & Ready
