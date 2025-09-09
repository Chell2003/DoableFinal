using DoableFinal.Models;

namespace DoableFinal.ViewModels
{
    public class TicketListViewModel
    {
        public IEnumerable<Ticket> Tickets { get; set; }
        public NotificationType NotificationType { get; set; } = NotificationType.General;
    }
}