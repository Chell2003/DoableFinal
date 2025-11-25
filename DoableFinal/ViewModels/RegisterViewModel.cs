using System.ComponentModel.DataAnnotations;

namespace DoableFinal.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; }

        [Required]
        [Display(Name = "Company Address")]
        public string CompanyAddress { get; set; }

        [Required]
        [Display(Name = "Designation")]
        public string Designation { get; set; }

        [Required]
        [Display(Name = "Mobile Number")]
        [DoableFinal.Validation.PhonePHAttribute(ErrorMessage = "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits).")]
        public string MobileNumber { get; set; }

        [Required]
        [Display(Name = "Birthday")]
        [DataType(DataType.Date)]
        [DoableFinal.Validation.MinAgeAttribute(18, ErrorMessage = "You must be at least 18 years old to register.")]
        public DateTime Birthday { get; set; }

        [Required]
        [Display(Name = "TIN Number")]
        [DoableFinal.Validation.TinAttribute(ErrorMessage = "Invalid TIN. Expected: XXX-XXX-XXX or XXX-XXX-XXX-XXX.")]
        public string TinNumber { get; set; }

        [Required]
        [Display(Name = "Company Type")]
        public string CompanyType { get; set; }
    }
} 