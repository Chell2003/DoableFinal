# Printable Reports - Quick Reference

## 🚀 Quick Start (5 Minutes)

### 1. Run Database Migration
```bash
cd f:\DoableFinal
Add-Migration AddTimeTrackingReports -OutputDir Migrations
Update-Database
```

### 2. Navigate to Reports
- Go to Client Projects page
- Click "Reports" button on any project card
- Start generating reports!

### 3. Add Time Entries (Optional)
```csharp
// In any service or controller
var timeEntry = new TimeEntry
{
    ProjectTaskId = 1,
    EmployeeId = "user-id",
    StartTime = DateTime.UtcNow.AddHours(-8),
    EndTime = DateTime.UtcNow,
    Description = "Task completed"
};
_context.TimeEntries.Add(timeEntry);
await _context.SaveChangesAsync();
```

---

## 📊 Report Types at a Glance

### Status Report
**URL:** `/Report/Status/{projectId}`  
**Shows:** Completed, In-Progress, Upcoming tasks  
**Ideal For:** Quick project status snapshot  
**Print:** ✅ Optimized

### Time Tracking Report
**URL:** `/Report/TimeTracking/{projectId}`  
**Shows:** Hours by task, employee, day, week  
**Ideal For:** Billing, resource planning  
**Print:** ✅ Optimized

### Workload Report
**URL:** `/Report/Workload/{projectId}`  
**Shows:** Task distribution, alerts, over-allocation  
**Ideal For:** Resource management, team balance  
**Print:** ✅ Optimized

### Progress Report
**URL:** `/Report/Progress/{projectId}`  
**Shows:** Health, milestones, completion %, risks  
**Ideal For:** Executive summary, status updates  
**Print:** ✅ Optimized

---

## 🎯 Usage Examples

### Generate Status Report
```csharp
var report = await _reportService.GenerateStatusReportAsync(projectId: 5);
// Access properties:
// report.CompletionPercentage
// report.CompletedTasks (List<TaskStatusItem>)
// report.InProgressTasks (List<TaskStatusItem>)
// report.UpcomingTasks (List<TaskStatusItem>)
```

### Generate Time Tracking Report
```csharp
var startDate = DateTime.UtcNow.AddMonths(-1);
var endDate = DateTime.UtcNow;

var report = await _reportService.GenerateTimeTrackingReportAsync(
    projectId: 5,
    startDate: startDate,
    endDate: endDate
);
// Access properties:
// report.TotalHours
// report.TaskTimes (List<TaskTimeItem>)
// report.EmployeeTimes (List<EmployeeTimeItem>)
// report.DailyBreakdown (List<DailyTimeItem>)
// report.WeeklyBreakdown (List<WeeklyTimeItem>)
```

### Generate Workload Report
```csharp
var report = await _reportService.GenerateWorkloadReportAsync(projectId: 5);
// Access properties:
// report.EmployeeWorkloads (List<EmployeeWorkloadItem>)
// report.OverallocationAlerts (List<OverallocationAlert>)
// report.AverageTasksPerEmployee
// report.MostLoadedEmployee
```

### Generate Progress Report
```csharp
var report = await _reportService.GenerateProgressReportAsync(projectId: 5);
// Access properties:
// report.CompletionPercentage
// report.Milestones (List<MilestoneItem>)
// report.TaskBreakdown (TaskBreakdownItem)
// report.HealthIndicator (ProjectHealthIndicator)
```

---

## 🔗 Navigation Paths

### From UI
1. Project List → Reports Button → Report Hub
2. Report Hub → Select Project → Select Report Type
3. Report View → Print/Print View Button

### Direct URLs
- Report Hub: `/Report`
- Status Report: `/Report/Status/5`
- Time Tracking: `/Report/TimeTracking/5`
- Workload Report: `/Report/Workload/5`
- Progress Report: `/Report/Progress/5`
- Print Views: Add "Print" suffix (e.g., `/Report/StatusPrint/5`)

---

## 📋 Report Fields Reference

### Status Report Fields
```
CompletionPercentage      // decimal (0-100)
TotalTasks               // int
CompletedTasks           // List<TaskStatusItem>
  - TaskId, Title, Priority, DueDate, CompletedAt, Status
InProgressTasks          // List<TaskStatusItem>
  - TaskId, Title, Priority, DueDate, Status
UpcomingTasks            // List<TaskStatusItem>
  - TaskId, Title, Priority, DueDate, Days Until Due
```

### Time Tracking Report Fields
```
TotalHours               // decimal
TaskTimes                // List<TaskTimeItem>
  - TaskId, Title, Hours, Status, % of Total
EmployeeTimes            // List<EmployeeTimeItem>
  - EmployeeId, Name, TotalHours, TaskCount, AvgPerTask
DailyBreakdown           // List<DailyTimeItem>
  - Date, Hours, TaskCount
WeeklyBreakdown          // List<WeeklyTimeItem>
  - WeekStart, WeekEnd, Hours, TaskCount
```

### Workload Report Fields
```
EmployeeWorkloads        // List<EmployeeWorkloadItem>
  - EmployeeId, Name, AssignedCount, CompletedCount
  - InProgressCount, OverdueCount, WorkloadPercentage
  - Tasks[] (AssignedTaskDetail)
OverallocationAlerts     // List<OverallocationAlert>
  - EmployeeId, Name, TaskCount, OverdueCount
  - SeverityLevel, Recommendation
AverageTasksPerEmployee  // decimal
MostLoadedEmployee       // string (name)
LeastLoadedEmployee      // string (name)
```

### Progress Report Fields
```
CompletionPercentage     // decimal (0-100)
Milestones               // List<MilestoneItem>
  - Index, Title, TargetDate, IsCompleted
  - CompletedDate, Status, Description
TaskBreakdown            // TaskBreakdownItem
  - TotalTasks, CompletedTasks, InProgressTasks
  - NotStartedTasks, OverdueTasks
HealthIndicator          // ProjectHealthIndicator
  - OverallHealth (Green/Yellow/Red)
  - ScheduleHealth, ResourceHealth, QualityHealth
  - Risks[], Achievements[]
```

---

## 🎨 Print Formatting

### Print Button Behavior
```csharp
// Standard button (browser print)
<button class="btn btn-primary" onclick="window.print()">
    <i class="fas fa-print"></i> Print
</button>

// Print View button (dedicated print page)
<a href="@Url.Action("StatusPrint", new { projectId = Model.ProjectId })" 
   class="btn btn-secondary" target="_blank">
    <i class="fas fa-file-pdf"></i> Print View
</a>
```

### Print CSS Classes
```css
/* Applied in all print views */
@media print {
    .btn { display: none; }              /* Hide buttons */
    .alert-light { display: none; }     /* Hide alerts */
    body { font-size: 10pt; }           /* Smaller font */
    .section { page-break-inside: avoid; }  /* Keep sections together */
}
```

---

## 🔒 Authorization Checks

### Access Control Logic
```csharp
private async Task<bool> UserCanAccessProject(int projectId)
{
    var project = await _context.Projects.FindAsync(projectId);
    if (project?.IsArchived == true) return false;
    
    if (userRole == "Admin") return true;
    if (userRole == "ProjectManager") 
        return project.ProjectManagerId == currentUser.Id;
    if (userRole == "Client") 
        return project.ClientId == currentUser.Id;
    
    return false;
}
```

### Who Can Access What?
| Role | Own Projects | Team Projects | All Projects |
|------|------------|---------------|--------------|
| Admin | ✅ | ✅ | ✅ |
| ProjectManager | ✅ | ✅ | ❌ |
| Client | ✅ | ❌ | ❌ |
| Employee | ❌ | ❌ | ❌ |

---

## 🐛 Troubleshooting Quick Guide

### Problem: "No data in reports"
**Solution:**
1. Verify project has tasks: `_context.Tasks.Where(t => t.ProjectId == id).Count()`
2. Verify user access: Check role and project assignment
3. Check archived status: `project.IsArchived == false`

### Problem: "Time tracking empty"
**Solution:**
1. Add TimeEntry records to database
2. Verify date range: `startDate <= endDate`
3. Check TimeEntry records: `_context.TimeEntries.Where(t => t.ProjectTaskId == id).Count()`

### Problem: "Print shows incomplete"
**Solution:**
1. Use Chrome/Edge browser
2. Enable "Background graphics" in print settings
3. Try Print View instead of Print button
4. Check page margins in print preview

### Problem: "Authorization error"
**Solution:**
1. Verify user is logged in
2. Check user role: Admin/ProjectManager/Client
3. Verify project access rights
4. Confirm project not archived

---

## 📊 Calculation Examples

### Health Indicator Score
```
IF overdue > 20% THEN "Delayed"
ELSE IF overdue > 10% THEN "At Risk"
ELSE "On Track"

Overall = Green if all good, Yellow if warning, Red if critical
```

### Workload Percentage
```
Employee Workload % = (Employee Tasks / Max Tasks) × 100

Example:
- Employee A: 8 tasks (8/10 = 80%)
- Employee B: 3 tasks (3/10 = 30%)
```

### Over-allocation Severity
```
IF overdue > 3 OR inProgress > 8 THEN "High"
ELSE IF overdue > 0 OR inProgress > 5 THEN "Medium"
ELSE "None"
```

---

## 📱 Mobile Considerations

### Responsive Design
- All reports use Bootstrap 5
- Tables stack on mobile
- Touch-friendly buttons (48px minimum)
- Optimized for landscape on tablets
- Print view: Use landscape for better fit

### Mobile Tips
1. Use landscape orientation for printing
2. Test print preview before printing
3. Zoom to 100% for best results
4. Use high-quality paper for color reports

---

## 🔄 Data Refresh

### Report Data Timing
- **Real-time:** All reports generated on-demand
- **Cache:** None (each request fresh data)
- **Frequency:** No automatic refresh
- **Manual:** Click report button to regenerate

### Best Practices
1. Generate reports during business hours
2. Archive old projects to keep data clean
3. Regularly update task status
4. Verify time entries are accurate
5. Review health indicators weekly

---

## 📚 Related Documentation

- **Full Guide:** `PRINTABLE_REPORT_GUIDE.md`
- **Implementation:** `PRINTABLE_REPORT_IMPLEMENTATION.md`
- **Models:** `Models/TimeEntry.cs`
- **ViewModels:** `ViewModels/ReportViewModels.cs`
- **Service:** `Services/ReportService.cs`
- **Controller:** `Controllers/ReportController.cs`

---

**Quick Reference Version:** 1.0  
**Last Updated:** December 5, 2025  
**Status:** Ready to Use
