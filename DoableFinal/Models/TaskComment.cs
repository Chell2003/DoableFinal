using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    /// <summary>
    /// Represents a comment on a project task in the system
    /// </summary>
    public class TaskComment
    {
        /// <summary>
        /// The unique identifier for the comment
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The ID of the project task this comment belongs to
        /// </summary>
        [Required]
        public int ProjectTaskId { get; set; }

        /// <summary>
        /// Navigation property to the associated project task
        /// </summary>
        public ProjectTask ProjectTask { get; set; }

        /// <summary>
        /// The actual content of the comment
        /// </summary>
        [Required]
        public string CommentText { get; set; }

        /// <summary>
        /// The ID of the user who created this comment
        /// </summary>
        [Required]
        public string CreatedById { get; set; }

        /// <summary>
        /// Navigation property to the user who created the comment
        /// </summary>
        public ApplicationUser CreatedBy { get; set; }

        /// <summary>
        /// The timestamp when the comment was created, automatically set to UTC time
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}