using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DoableFinal.ViewModels
{
    public class CreateProjectViewModel
    {
        [Required]
        [Display(Name = "Project Name")]
        public string Name { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Required]
        public string Status { get; set; }

        [Required]
        public string Priority { get; set; }

        [Required]
        [Display(Name = "Client")]
        public string ClientId { get; set; }

        [Required]
        [Display(Name = "Project Manager")]
        public string ProjectManagerId { get; set; }

        // Dropdown lists
        public List<SelectListItem> Clients { get; set; }
        public List<SelectListItem> ProjectManagers { get; set; }

        public CreateProjectViewModel()
        {
            Clients = new List<SelectListItem>();
            ProjectManagers = new List<SelectListItem>();
            StartDate = DateTime.Today;
            EndDate = DateTime.Today.AddMonths(1);
            Status = "Not Started";
            Priority = "Medium";
        }
    }
} 