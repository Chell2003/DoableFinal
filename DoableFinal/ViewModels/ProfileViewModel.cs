using System.ComponentModel.DataAnnotations;

namespace DoableFinal.ViewModels
{
    public class ProfileViewModel
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

        public string Role { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Last Login")]
        public DateTime? LastLoginAt { get; set; }

        [Display(Name = "Email Notifications")]
        public bool EmailNotificationsEnabled { get; set; }

        // Client-specific fields
        [Display(Name = "Company Name")]
        public string? CompanyName { get; set; }

        [Display(Name = "Company Address")]
        public string? CompanyAddress { get; set; }

        [Display(Name = "Company Type")]
        public string? CompanyType { get; set; }

        [Display(Name = "Designation")]
        public string? Designation { get; set; }

        [Display(Name = "Mobile Number")]
        [DoableFinal.Validation.PhonePHAttribute(ErrorMessage = "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits).")]
        public string? MobileNumber { get; set; }

        [Display(Name = "TIN Number")]
        [DoableFinal.Validation.TinAttribute(ErrorMessage = "Invalid TIN. Expected: XXX-XXX-XXX or XXX-XXX-XXX-XXX.")]
        public string? TinNumber { get; set; }

        // Employee / Project Manager additional fields
        [Display(Name = "Residential Address")]
        public string? ResidentialAddress { get; set; }

        [Display(Name = "Birthday")]
        [DoableFinal.Validation.MinAgeAttribute(18, ErrorMessage = "You must be at least 18 years old.")]
        public DateTime? Birthday { get; set; }

        [Display(Name = "Pag-IBIG Account")]
        [DoableFinal.Validation.PagIbigAttribute(ErrorMessage = "Invalid Pag-IBIG MID. Expected 12 numeric digits.")]
        public string? PagIbigAccount { get; set; }

        [Display(Name = "Position")]
        public string? Position { get; set; }

        // Admin / account fields
        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Archived")]
        public bool IsArchived { get; set; }

        [Display(Name = "Archived At")]
        public DateTime? ArchivedAt { get; set; }
    }
} 