using Microsoft.AspNetCore.Identity;

namespace ReadyPlayerOne.Models
{
    public class ApplicationUser: IdentityUser
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }
    }
}
