using DoableFinal.Data;
using DoableFinal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DoableFinal.Services
{
    /// <summary>
    /// Background service that runs once per hour and sends a deadline reminder
    /// notification (+ email) to each employee whose task is due exactly tomorrow.
    /// A duplicate-guard in NotificationService.CreateNotification prevents the same
    /// reminder from being sent more than once in any 5-second window, but we also
    /// store a "ReminderSent" flag via the Notification title so the check below
    /// only fires once per task per day.
    /// </summary>
    public class DeadlineReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DeadlineReminderService> _logger;

        // How often to check (every hour is fine; the date-window check avoids duplicates)
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

        public DeadlineReminderService(
            IServiceScopeFactory scopeFactory,
            ILogger<DeadlineReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DeadlineReminderService started.");

            // Wait a short grace period after startup before the first run
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SendDeadlineRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in DeadlineReminderService.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        public async Task SendDeadlineRemindersAsync(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

            var now = DateTime.UtcNow;

            // Window: tasks due any time in the next 24–48 hours
            // (catches tasks regardless of exact time-of-day stored in DueDate)
            var windowStart = now;
            var windowEnd   = now.AddHours(48);

            _logger.LogInformation(
                "DeadlineReminderService: scanning tasks due between {Start} and {End} UTC.",
                windowStart, windowEnd);

            // Find all incomplete tasks whose DueDate is within the next 24-48h
            var dueTasks = await db.Tasks
                .Include(t => t.TaskAssignments)
                .Include(t => t.Project)
                .Where(t =>
                    t.Status != "Completed" &&
                    !t.IsArchived &&
                    t.DueDate >= windowStart &&
                    t.DueDate <= windowEnd)
                .ToListAsync(ct);

            _logger.LogInformation(
                "DeadlineReminderService: {Count} task(s) due in window.", dueTasks.Count);

            foreach (var task in dueTasks)
            {
                var assigneeIds = task.TaskAssignments.Select(ta => ta.EmployeeId).ToList();

                _logger.LogInformation(
                    "Task '{Title}' (Id={Id}) has {N} assignee(s).",
                    task.Title, task.Id, assigneeIds.Count);

                foreach (var employeeId in assigneeIds)
                {
                    // De-duplicate: skip if we already sent this reminder in the last 20 hours
                    var alreadySent = await db.Notifications.AnyAsync(n =>
                        n.UserId == employeeId &&
                        n.Title == $"Deadline Tomorrow: {task.Title}" &&
                        n.CreatedAt >= now.AddHours(-20),
                        ct);

                    if (alreadySent)
                    {
                        _logger.LogInformation(
                            "Skipping duplicate reminder for employee {Id}, task '{Title}'.",
                            employeeId, task.Title);
                        continue;
                    }

                    var projectName = task.Project?.Name ?? "your project";
                    var dueFormatted = task.DueDate.ToString("MMM dd, yyyy h:mm tt") + " UTC";

                    await notificationService.CreateNotification(
                        userId: employeeId,
                        title: $"Deadline Tomorrow: {task.Title}",
                        message: $"Your task "{task.Title}" in project "{projectName}" is due on {dueFormatted}. Please make sure it's completed on time.",
                        link: $"/Employee/TaskDetails/{task.Id}"
                    );

                    _logger.LogInformation(
                        "Deadline reminder sent → employee {EmployeeId}, task '{TaskTitle}' (due {DueDate}).",
                        employeeId, task.Title, dueFormatted);
                }
            }
        }
    }
}
