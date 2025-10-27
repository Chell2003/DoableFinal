using Microsoft.AspNetCore.Mvc.Rendering;

namespace DoableFinal.ViewModels
{
    public class TicketStatusEditViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string CurrentStatus { get; set; }
        public List<SelectListItem> AvailableStatuses { get; set; }
    }
}