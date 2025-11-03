using DoableFinal.Data;
using DoableFinal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using System.Net;

namespace DoableFinal.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public NotificationService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task NotifyProjectUpdateAsync(Project project, string message)
        {
            // Skip archive/unarchive notifications for clients
            if (message.Contains("archived") || message.Contains("unarchived"))
                return;
                
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

        public async Task NotifyTaskUpdateAsync(ProjectTask task, string message, string? specificUserId = null)
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == task.ProjectId);

            if (project == null)
                return;

            // Get assigned employees if not sending to a specific user
            var assigneeIds = new List<string>();
            if (specificUserId != null)
            {
                assigneeIds.Add(specificUserId);
            }
            else
            {
                // Get all assigned employees for this task
                var taskWithAssignments = await _context.Tasks
                    .Include(t => t.TaskAssignments)
                    .FirstOrDefaultAsync(t => t.Id == task.Id);

                if (taskWithAssignments?.TaskAssignments != null)
                {
                    assigneeIds = taskWithAssignments.TaskAssignments.Select(ta => ta.EmployeeId).ToList();
                }
            }

            foreach (var userId in assigneeIds)
            {
                // Skip archive/unarchive notifications for clients
                if ((message.Contains("archived") || message.Contains("unarchived")) && userId == project.ClientId)
                    continue;
                    
                var notificationLink = userId == project.ClientId 
                    ? $"/Client/TaskDetails/{task.Id}"
                    : $"/Employee/TaskDetails/{task.Id}";

                var notification = new Notification
                {
                    UserId = userId,
                    Title = $"Task Update: {task.Title}",
                    Message = message,
                    Link = notificationLink,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Notifications.Add(notification);
            }

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

        public async Task CreateNotification(string userId, string title, string message, string? link = default)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Link = link,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                Type = NotificationType.General
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task SendEmailNotificationAsync(string email, string subject, string message)
        {
            try
            {
                var smtpSettings = _configuration.GetSection("SmtpSettings");
                var host = smtpSettings["Host"] ?? throw new InvalidOperationException("SMTP Host not configured");
                var portStr = smtpSettings["Port"] ?? throw new InvalidOperationException("SMTP Port not configured");
                var port = int.Parse(portStr);
                var username = smtpSettings["Username"] ?? throw new InvalidOperationException("SMTP Username not configured");
                var password = smtpSettings["Password"] ?? throw new InvalidOperationException("SMTP Password not configured");
                var fromEmail = smtpSettings["FromEmail"] ?? throw new InvalidOperationException("SMTP FromEmail not configured");
                var fromName = smtpSettings["FromName"] ?? "Doable Task Management";

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = message,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                // Log the error but don't throw it - email notification is not critical
                Console.WriteLine($"Error sending email notification: {ex.Message}");
            }
        }
    }
}