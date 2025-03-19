using System.ComponentModel.DataAnnotations;

namespace DoableFinal.Models
{
    public class ProjectTeam
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public Project Project { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public string Role { get; set; } // Team Member, Developer, Designer, etc.
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
} 