using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.DTOs.GameSessions
{
    public class GameSessionResponse
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

        public List<TeamResponse> Teams { get; set; } = new();
        public List<SessionParticipantResponse> Participants { get; set; } = new();
    }

    public class TeamResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MemberCount { get; set; }
    }

    public class SessionParticipantResponse
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public Guid? TeamId { get; set; }
        public bool IsReady { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}