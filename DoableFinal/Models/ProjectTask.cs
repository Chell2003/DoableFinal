using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
namespace DoableFinal.Models
{
    public class ProjectTask
    {
        public int Id { get; set; }
        [Required]
        [StringLength(200)]
        public string Title { get; set; }
        [StringLength(1000)]
        public string Description { get; set; }
        [Required]
        public string Status { get; set; } = "Not Started";
        [Required]
        public string Priority { get; set; } = "Medium";
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        [Required]
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        [Required]
        public int ProjectId { get; set; }
        public Project Project { get; set; }

        // Remove this single assignment property
        // [Required]
        // public string AssignedToId { get; set; }
        // public ApplicationUser AssignedTo { get; set; }

        // Add the collection of task assignments
        public virtual ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();

        [Required]
        public string CreatedById { get; set; }
        public ApplicationUser CreatedBy { get; set; }
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}