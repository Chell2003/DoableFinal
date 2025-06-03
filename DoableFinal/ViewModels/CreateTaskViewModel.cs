using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using DoableFinal.Validation;

namespace DoableFinal.ViewModels
{
    public class CreateTaskViewModel
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        [ProjectDateRange("ProjectId", true, ErrorMessage = "Start date must be within the project timeline")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        [ProjectDateRange("ProjectId", false, ErrorMessage = "Due date must be within the project timeline")]
        public DateTime DueDate { get; set; }

        [Required]
        public string Status { get; set; } = "Not Started";

        [Required]
        public string Priority { get; set; } = "Medium";

        [Required]
        [Display(Name = "Project")]
        public int ProjectId { get; set; }

        // Properties to store project date constraints
        public DateTime ProjectStartDate { get; set; }
        public DateTime ProjectEndDate { get; set; }

        [Display(Name = "Assigned To")]
        public List<string> AssignedToIds { get; set; } = new List<string>();

        public List<SelectListItem> Projects { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Employees { get; set; } = new List<SelectListItem>();

        // Dynamic data for Select2
        public object AvailableEmployees { get; set; }

        public CreateTaskViewModel()
        {
            Status = "Not Started";
            Priority = "Medium";
            StartDate = DateTime.Today;
            DueDate = DateTime.Today.AddDays(7);
        }
    }
}