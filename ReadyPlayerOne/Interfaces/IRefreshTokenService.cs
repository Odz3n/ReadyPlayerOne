using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Interfaces
{
    public interface IRefreshTokenService
    {
        Task<(string RawToken, RefreshToken Entity)> CreateAsync(ApplicationUser user);
        Task<(string RawToken, ApplicationUser User)?> RotateAsync(string rawToken);
        Task RevokeAsync(string rawToken);
    }
}
