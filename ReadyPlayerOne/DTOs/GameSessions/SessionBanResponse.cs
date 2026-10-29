namespace ReadyPlayerOne.DTOs.GameSessions
{
    public class SessionBanResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string BannedByUserId { get; set; } = string.Empty;
        public string BannedByUserName { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
