using DoableFinal.Data;
using DoableFinal.Models;
using Microsoft.EntityFrameworkCore;

namespace DoableFinal.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task NotifyProjectUpdateAsync(Project project, string message)
        {
            var notification = new Notification
            {
                UserId = project.ClientId,
                Title = $"Project Update: {project.Name}",
                Message = message,
                Link = $"/Client/ProjectDetails/{project.Id}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task NotifyTaskUpdateAsync(ProjectTask task, string message)
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == task.ProjectId);

            if (project == null)
                return;

            var notification = new Notification
            {
                UserId = project.ClientId,
                Title = $"Task Update: {task.Title}",
                Message = message,
                Link = $"/Client/TaskDetails/{task.Id}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                return false;

            notification.IsRead = true;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}