using Microsoft.AspNetCore.Identity;

namespace ReadyPlayerOne.Models
{
    public class ApplicationUser: IdentityUser
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }

        public ICollection<SessionParticipant> Sessions { get; set; } = new List<SessionParticipant>();
        public ICollection<GameSession> HostedSessions { get; set; } = new List<GameSession>();
    }
}
