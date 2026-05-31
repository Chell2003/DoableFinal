using System.ComponentModel.DataAnnotations;

namespace DoableFinal.ViewModels
{
    public class VerifyOtpViewModel
    {
        [Required]
        public string Email { get; set; }


        public bool RememberMe { get; set; }

        [Required]
        [Display(Name = "Verification Code")]
        public string OtpCode { get; set; }
    }
}