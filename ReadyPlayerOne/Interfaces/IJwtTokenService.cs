using ReadyPlayerOne.DTOs.Auth;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Interfaces
{
    public interface IJwtTokenService
    {
        Task<AuthResponse> GenerateTokenAsync(ApplicationUser user);
    }
}
