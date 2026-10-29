namespace ReadyPlayerOne.Models
{
    public class SessionBan
    {
        public Guid Id { get; set; }
        public Guid GameSessionId { get; set; }
        public GameSession GameSession { get; set; } = null!;
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;
        public string BannedByUserId { get; set; } = string.Empty;
        public ApplicationUser BannedByUser { get; set; } = null!;
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
