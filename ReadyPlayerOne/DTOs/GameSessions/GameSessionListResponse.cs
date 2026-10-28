using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.DTOs.GameSessions
{
    // GET /api/game-sessions
    public class GameSessionListResponse
    {
        public Guid Id { get; set; }
        public Guid GameId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public string HostId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public SessionMode Mode { get; set; }
        public GameSessionStatus Status { get; set; }

        public int MaxPlayers { get; set; }
        public int CurrentPlayers { get; set; }

        public int? RequiredTeams { get; set; }
        public int? PlayersPerTeam { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
    }
}
