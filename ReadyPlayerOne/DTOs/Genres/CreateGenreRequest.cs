using System.ComponentModel.DataAnnotations;

namespace ReadyPlayerOne.DTOs.Genres
{
    public class CreateGenreRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
