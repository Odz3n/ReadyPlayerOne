using System.ComponentModel.DataAnnotations;

namespace ReadyPlayerOne.DTOs.Genres
{
    public class UpdateGenreRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}