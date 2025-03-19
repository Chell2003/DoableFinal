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

        [Required]
        [Display(Name = "Assigned To")]
        public string AssignedToId { get; set; }

        // Dropdown lists
        public List<SelectListItem> Projects { get; set; }
        public List<SelectListItem> Employees { get; set; }

        public CreateTaskViewModel()
        {
            Projects = new List<SelectListItem>();
            Employees = new List<SelectListItem>();
            StartDate = DateTime.Today;
            DueDate = DateTime.Today.AddDays(7);
            Status = "Not Started";
            Priority = "Medium";
        }
    }
} 