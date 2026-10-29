using System.ComponentModel.DataAnnotations;

namespace ReadyPlayerOne.DTOs.GameSessions
{
    public class BanUserRequest
    {
        [StringLength(500)]
        public string? Reason { get; set; }
    }
}
