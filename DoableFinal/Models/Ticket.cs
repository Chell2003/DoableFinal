using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class Ticket
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public string Priority { get; set; } // Low, Medium, High, Critical

        [Required]
        public string Status { get; set; } // Open, In Progress, Resolved, Closed

        [Required]
        public string Type { get; set; } // Bug, Feature Request, Support, Other

        public string? AssignedToId { get; set; }
        public ApplicationUser AssignedTo { get; set; }

        [Required]
        public string CreatedById { get; set; }
        public ApplicationUser CreatedBy { get; set; }

        public int? ProjectId { get; set; }
        public Project Project { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public virtual ICollection<TicketComment> Comments { get; set; }
        public virtual ICollection<TicketAttachment> Attachments { get; set; }
    }
}
