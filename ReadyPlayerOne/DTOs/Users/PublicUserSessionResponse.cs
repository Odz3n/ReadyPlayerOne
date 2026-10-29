using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.DTOs.Users
{
    public class PublicUserSessionResponse
    {
        public Guid Id { get; set; }
        public Guid GameId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public GameSessionStatus Status { get; set; }
        public SessionMode Mode { get; set; }
    }
}
