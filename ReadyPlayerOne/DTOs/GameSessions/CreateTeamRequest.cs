using System.ComponentModel.DataAnnotations;

namespace ReadyPlayerOne.DTOs.GameSessions
{
    public class CreateTeamRequest
    {
        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;
    }
}