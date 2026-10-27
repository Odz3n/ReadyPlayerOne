using System.ComponentModel.DataAnnotations;

namespace ReadyPlayerOne.DTOs.Games
{
    public class UpdateGameRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public Guid GenreId { get; set; }

        [Url]
        [MaxLength(2048)]
        public string? CoverImageUrl { get; set; }
    }
}
