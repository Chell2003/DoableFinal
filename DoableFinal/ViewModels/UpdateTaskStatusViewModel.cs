using System.ComponentModel.DataAnnotations;

namespace DoableFinal.ViewModels
{
    public class UpdateTaskStatusViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; }
    }
} 