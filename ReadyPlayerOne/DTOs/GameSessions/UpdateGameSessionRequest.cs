using System.ComponentModel.DataAnnotations;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.DTOs.GameSessions
{
    public class UpdateGameSessionRequest
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public SessionMode Mode { get; set; }

        [Range(2, 100)]
        public int MaxPlayers { get; set; }

        public int? RequiredTeams { get; set; }
        public int? PlayersPerTeam { get; set; }
    }
}