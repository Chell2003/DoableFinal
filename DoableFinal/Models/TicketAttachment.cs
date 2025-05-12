using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class TicketAttachment
    {
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; }

        [Required]
        public string FilePath { get; set; }

        public string FileType { get; set; }

        public long FileSize { get; set; }

        public int TicketId { get; set; }
        public Ticket Ticket { get; set; }

        public string UploadedById { get; set; }
        public ApplicationUser UploadedBy { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
