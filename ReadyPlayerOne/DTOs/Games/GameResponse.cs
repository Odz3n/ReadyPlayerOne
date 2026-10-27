namespace ReadyPlayerOne.DTOs.Games
{
    public class GameResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
    }
}
