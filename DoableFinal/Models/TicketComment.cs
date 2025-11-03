using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class TicketComment
    {
        public int Id { get; set; }

        [Required]
        [System.ComponentModel.DataAnnotations.Schema.Column("Content")]
        public string CommentText { get; set; }

        public int TicketId { get; set; }
        public Ticket Ticket { get; set; }

        public string CreatedById { get; set; }
        public ApplicationUser CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public bool IsArchived { get; set; }
    }
}
