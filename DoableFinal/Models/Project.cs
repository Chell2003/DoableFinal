using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public string Description { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } // Not Started, In Progress, Completed, On Hold

        [Required]
        public string Priority { get; set; } // Low, Medium, High

        [Required]
        public string ClientId { get; set; }
        public ApplicationUser Client { get; set; }

        public string ProjectManagerId { get; set; }
        public ApplicationUser ProjectManager { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<ProjectTask> Tasks { get; set; }
        public virtual ICollection<ProjectTeam> ProjectTeams { get; set; }
    }
} 