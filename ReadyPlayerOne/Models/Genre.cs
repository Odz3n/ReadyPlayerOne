namespace ReadyPlayerOne.Models
{
    public class Genre
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;

        public ICollection<Game> Games { get; set; } = new List<Game>();
    }
}
