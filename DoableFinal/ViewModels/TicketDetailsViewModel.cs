using DoableFinal.Models;

namespace DoableFinal.ViewModels
{
    public class TicketDetailsViewModel
    {
        public Ticket Ticket { get; set; }
        public List<TicketComment> Comments { get; set; }
        public List<TicketAttachment> Attachments { get; set; }
        public string NewComment { get; set; }
    }
}
