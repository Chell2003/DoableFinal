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

        public string? ProofFilePath { get; set; } // Path to the uploaded file
        public string? Remarks { get; set; } // Remarks for task completion
        // Reason provided by the Project Manager when disapproving a proof
        public string? DisapprovalRemark { get; set; }
        public DateTime? DisapprovedAt { get; set; }
        public bool IsConfirmed { get; set; } = false; // Whether the task is confirmed by the Project Manager
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }

        public virtual ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();

        [Required]
        public string CreatedById { get; set; }
        public ApplicationUser CreatedBy { get; set; }
        public virtual ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
        public virtual ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
    }
}