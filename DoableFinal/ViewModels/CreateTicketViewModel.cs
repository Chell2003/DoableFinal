using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DoableFinal.ViewModels
{
    public class CreateTicketViewModel
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public string Priority { get; set; }

        [Required]
        public string Type { get; set; }

        public int? ProjectId { get; set; }
        public string? AssignedToId { get; set; }

        public List<SelectListItem> Projects { get; set; }
        public List<SelectListItem> Assignees { get; set; }
        public List<SelectListItem> PriorityLevels { get; set; }
        public List<SelectListItem> TicketTypes { get; set; }
    }
}
