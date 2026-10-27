using System.ComponentModel.DataAnnotations;

namespace ReadyPlayerOne.DTOs.Games
{
    public class CreateGameRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }
    }
}
