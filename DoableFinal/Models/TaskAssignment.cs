using System;
using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class TaskAssignment
    {
        public int Id { get; set; }

        [Required]
        public int ProjectTaskId { get; set; }
        public ProjectTask ProjectTask { get; set; }

        [Required]
        public string EmployeeId { get; set; }
        public ApplicationUser Employee { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}