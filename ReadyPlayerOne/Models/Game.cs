namespace ReadyPlayerOne.Models
{
    public class Game
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Description  { get; set; }

        public ICollection<GameSession> Sessions { get; set; } = new List<GameSession>();
    }
}
