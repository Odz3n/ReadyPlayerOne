namespace ReadyPlayerOne.Models
{
    public enum GameSessionStatus
    {
        Waiting,
        InProgress,
        Completed,
        Cancelled
    }
    public enum SessionMode
    {
        FreeForAll,
        TeamBased,
        Cooperative
    }
    public class GameSession
    {
        public Guid Id { get; set; }

        public Guid GameId { get; set; }
        public Game Game { get; set; } = null!;

        public string HostId { get; set; } = string.Empty;
        public ApplicationUser Host { get; set; } = null!;

        public string Name { get; set; } = string.Empty;

        public SessionMode Mode { get; set; }
        public GameSessionStatus Status { get; set; } = GameSessionStatus.Waiting;

        public int MaxPlayers { get; set; }
        public int? RequiredTeams { get; set; }
        public int? PlayersPerTeam { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        public ICollection<Team> Teams { get; set; } = new List<Team>();
        public ICollection<SessionParticipant> Participants { get; set; } = new List<SessionParticipant>();
    }
}
