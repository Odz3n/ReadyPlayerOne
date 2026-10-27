namespace ReadyPlayerOne.Models
{
    public class Team
    {
        public Guid Id { get; set; }

        public Guid GameSessionId { get; set; }
        public GameSession GameSession { get; set; } = null!;

        public string Name { get; set; } = string.Empty;

        public ICollection<SessionParticipant> Members { get; set; } = new List<SessionParticipant>();
    }
}
