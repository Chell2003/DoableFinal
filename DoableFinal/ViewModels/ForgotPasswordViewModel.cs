using System.ComponentModel.DataAnnotations;

namespace DoableFinal.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
