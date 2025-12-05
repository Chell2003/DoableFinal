using System;
using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class TimeEntry
    {
        public int Id { get; set; }

        [Required]
        public int ProjectTaskId { get; set; }
        public ProjectTask ProjectTask { get; set; }

        [Required]
        public string EmployeeId { get; set; }
        public ApplicationUser Employee { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
