using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class TaskComment
    {
        public int Id { get; set; }

        [Required]
        public int ProjectTaskId { get; set; }
        public ProjectTask ProjectTask { get; set; }

        [Required]
        public string CommentText { get; set; } // This is the text of the comment

        [Required]
        public string CreatedById { get; set; } // This links the comment to the user who created it
        public ApplicationUser CreatedBy { get; set; } // Navigation property for the user

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Timestamp for when the comment was created
    }
}