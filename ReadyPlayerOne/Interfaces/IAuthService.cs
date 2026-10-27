using Microsoft.AspNetCore.Identity;
using ReadyPlayerOne.DTOs.Auth;

namespace ReadyPlayerOne.Interfaces
{
    public interface IAuthService
    {
        Task<IdentityResult> RegisterAsync(RegisterRequest request);
        Task<AuthSessionResult?> LoginAsync(LoginRequest request);
        Task<AuthSessionResult?> RefreshAsync(string refreshToken);
        Task LogoutAsync(string refreshToken);
        Task<IdentityResult> ConfirmEmailAsync(string userId, string encodedToken);
        Task ResendConfirmationAsync(string email);
    }
}
