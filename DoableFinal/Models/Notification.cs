using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Message { get; set; }

        public string? Link { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public NotificationType Type { get; set; }
    }

    public enum NotificationType
    {
        ProjectUpdate,
        TaskUpdate,
        General
    }
}