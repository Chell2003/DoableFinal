using Microsoft.AspNetCore.Identity;

namespace DoableFinal.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Role { get; set; } = "Employee"; // Admin, Employee, or Client
        public bool IsActive { get; set; } = true;
        
        // Employee and Project Manager fields
        public string? ResidentialAddress { get; set; }
        public DateTime? Birthday { get; set; }
        public string? PagIbigAccount { get; set; }
        public string? Position { get; set; }
        
        // Client-specific fields
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? Designation { get; set; }
        public string? MobileNumber { get; set; }
        public string? TinNumber { get; set; }
        public string? CompanyType { get; set; } // sole prop, partnership, corporation
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public bool EmailNotificationsEnabled { get; set; }
        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }
    }
} 