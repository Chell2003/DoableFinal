using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class Comment
    {
        public int Id { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public int TaskId { get; set; }
        public ProjectTask Task { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
    }
} 