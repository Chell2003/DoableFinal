using System.ComponentModel.DataAnnotations;

namespace DoableFinal.ViewModels
{
    public class CreateUserViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, ErrorMessage = "The password must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }        [Required]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "Residential Address")]
        public string? ResidentialAddress { get; set; }

        [Display(Name = "Mobile Number")]
        [Phone]
        public string? MobileNumber { get; set; }

        [Display(Name = "Birthday")]
        [DataType(DataType.Date)]
        public DateTime? Birthday { get; set; }

        [Display(Name = "TIN Number")]
        public string? TinNumber { get; set; }

        [Display(Name = "Email Notifications")]
        public bool EmailNotificationsEnabled { get; set; } = true;

        [Display(Name = "Pag-IBIG Account")]
        public string? PagIbigAccount { get; set; }

        [Display(Name = "Position")]
        public string? Position { get; set; }

        // Client fields
        [Display(Name = "Company Name")]
        public string? CompanyName { get; set; }

        [Display(Name = "Company Address")]
        public string? CompanyAddress { get; set; }

        [Display(Name = "Designation")]
        public string? Designation { get; set; }

        [Display(Name = "Company Type")]
        public string? CompanyType { get; set; }
    }
}
