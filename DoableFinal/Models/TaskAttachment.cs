using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class TaskAttachment
    {
        public int Id { get; set; }

        [Required]
        public int ProjectTaskId { get; set; }
        public ProjectTask ProjectTask { get; set; }

        [Required]
        public string FilePath { get; set; }

        [Required]
        public string UploadedById { get; set; }
        public ApplicationUser UploadedBy { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}