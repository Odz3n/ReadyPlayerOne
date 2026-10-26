using Microsoft.AspNetCore.Identity;
using ReadyPlayerOne.DTOs.Auth;

namespace ReadyPlayerOne.Interfaces
{
    public interface IAuthService
    {
        Task<IdentityResult> RegisterAsync(RegisterRequest request);
        Task<AuthResponse?> LoginAsync(LoginRequest request);
    }
}
