using System;
using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class HomePageSection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string SectionKey { get; set; } // e.g., "hero-title", "hero-body", "feature-1-title", etc.

        [Required]
        [StringLength(500)]
        public string DisplayName { get; set; } // e.g., "Hero Section - Title"

        [Required]
        public string Content { get; set; } // The editable HTML content

        public string? IconClass { get; set; } // e.g., "bi bi-kanban" for feature sections

        public int? SectionOrder { get; set; } // For ordering features

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public string? UpdatedBy { get; set; }
    }
}
