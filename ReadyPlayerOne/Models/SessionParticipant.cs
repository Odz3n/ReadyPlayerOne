namespace ReadyPlayerOne.Models
{
    public class SessionParticipant
    {
        public Guid Id { get; set; }

        public Guid GameSessionId { get; set; }
        public GameSession GameSession { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public Guid? TeamId { get; set; }
        public Team? Team { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool IsReady { get; set; }
    }
}
