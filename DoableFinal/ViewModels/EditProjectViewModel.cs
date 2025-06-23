using System.ComponentModel.DataAnnotations;

namespace DoableFinal.ViewModels
{
    public class EditProjectViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Required]
        public string Status { get; set; }


        [Required]
        [Display(Name = "Client")]
        public string? ClientId { get; set; }

        // The name of the client for display purposes
        public string? ClientName { get; set; }

        [Required]
        [Display(Name = "Project Manager")]
        public string? ProjectManagerId { get; set; }
    }
} 