using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace DoableFinal.ViewModels
{
    public class CreateTaskViewModel
    {
        [Required]
        public string Title { get; set; }
        [Required]
        public string Description { get; set; }
        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }
        [Required]
        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }
        [Required]
        public string Status { get; set; }
        [Required]
        public string Priority { get; set; }
        [Required]
        [Display(Name = "Project")]
        public int ProjectId { get; set; }

        // Change this property to a list of selected employee IDs
        [Display(Name = "Assigned To")]
        public List<string> AssignedToIds { get; set; }

        // Dropdown lists
        public List<SelectListItem> Projects { get; set; }
        public List<SelectListItem> Employees { get; set; }

        public CreateTaskViewModel()
        {
            Projects = new List<SelectListItem>();
            Employees = new List<SelectListItem>();
            AssignedToIds = new List<string>();
            StartDate = DateTime.Today;
            DueDate = DateTime.Today.AddDays(7);
            Status = "Not Started";
            Priority = "Medium";
        }
    }
}