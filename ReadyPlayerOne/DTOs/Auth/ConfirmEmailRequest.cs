using System.ComponentModel.DataAnnotations;

namespace ReadyPlayerOne.DTOs.Auth
{
    public class ConfirmEmailRequest
    {
        public required string UserId { get; set; }
        public required string Token { get; set; }
    }
}
