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

        private async Task SendDeadlineRemindersAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

            var now = DateTime.UtcNow;
            // Define "tomorrow" as the calendar day that starts 24 h from now
            var tomorrowStart = now.Date.AddDays(1);       // e.g. 2026-06-11 00:00
            var tomorrowEnd   = tomorrowStart.AddDays(1);  // e.g. 2026-06-12 00:00

            // Find all incomplete tasks whose DueDate falls in tomorrow's window
            var dueTasks = await db.Tasks
                .Include(t => t.TaskAssignments)
                .Include(t => t.Project)
                .Where(t =>
                    t.Status != "Completed" &&
                    !t.IsArchived &&
                    t.DueDate >= tomorrowStart &&
                    t.DueDate < tomorrowEnd)
                .ToListAsync(ct);

            if (!dueTasks.Any())
                return;

            _logger.LogInformation("DeadlineReminderService: {Count} task(s) due tomorrow.", dueTasks.Count);

            foreach (var task in dueTasks)
            {
                var assigneeIds = task.TaskAssignments.Select(ta => ta.EmployeeId).ToList();

                foreach (var employeeId in assigneeIds)
                {
                    // De-duplicate: skip if we already sent this reminder today
                    var alreadySent = await db.Notifications.AnyAsync(n =>
                        n.UserId == employeeId &&
                        n.Title == $"Deadline Tomorrow: {task.Title}" &&
                        n.CreatedAt >= now.Date,
                        ct);

                    if (alreadySent)
                        continue;

                    var projectName = task.Project?.Name ?? "your project";
                    var dueFormatted = task.DueDate.ToLocalTime().ToString("MMM dd, yyyy h:mm tt");

                    await notificationService.CreateNotification(
                        userId: employeeId,
                        title: $"Deadline Tomorrow: {task.Title}",
                        message: $"Your task \"{task.Title}\" in project \"{projectName}\" is due tomorrow on {dueFormatted}. Please make sure it's completed on time.",
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
