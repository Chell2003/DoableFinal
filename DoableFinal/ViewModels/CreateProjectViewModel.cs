using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using DoableFinal.Validation;

namespace DoableFinal.ViewModels
{
    public class CreateProjectViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        [FutureDate(ErrorMessage = "Start date cannot be in the past")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Required]
        public string Status { get; set; } = "Not Started";

        [Required]
        [Display(Name = "Client")]
        public string ClientId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Project Manager")]
        public string ProjectManagerId { get; set; } = string.Empty;

        public List<SelectListItem> Clients { get; set; }
        public List<SelectListItem> ProjectManagers { get; set; }

        public CreateProjectViewModel()
        {
            Clients = new List<SelectListItem>();
            ProjectManagers = new List<SelectListItem>();
            StartDate = DateTime.Today;
            EndDate = DateTime.Today.AddMonths(1);
        }
    }
}