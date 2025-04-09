using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace DoableFinal.ViewModels
{
    public class EditTaskViewModel
    {
        public int Id { get; set; }

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

        // Updated to support multiple employees
        [Display(Name = "Assigned To")]
        public List<string> AssignedToIds { get; set; }

        public EditTaskViewModel()
        {
            AssignedToIds = new List<string>();
        }
    }
}