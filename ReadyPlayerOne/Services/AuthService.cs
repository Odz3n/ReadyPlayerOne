using Microsoft.AspNetCore.Identity;
using ReadyPlayerOne.DTOs.Auth;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IRefreshTokenService _refreshTokenService;
        public AuthService(
            UserManager<ApplicationUser> userManager,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService refreshTokenService)
        {
            _userManager = userManager;
            _jwtTokenService = jwtTokenService;
            _refreshTokenService = refreshTokenService;
        }
        public async Task<AuthSessionResult?> LoginAsync(LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return null;
            if (await _userManager.IsLockedOutAsync(user))
                return null;

            var validPassword = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!validPassword)
                return null;

            var accessToken = await _jwtTokenService.GenerateTokenAsync(user);
            var (refreshToken, _) = await _refreshTokenService.CreateAsync(user);

            return new AuthSessionResult
            {
                Access = accessToken,
                RefreshToken = refreshToken
            };
        }
        public async Task<IdentityResult> RegisterAsync(RegisterRequest request)
        {
            var user = new ApplicationUser
            {
                UserName = request.UserName,
                DisplayName = request.UserName,
                Email = request.Email
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return result;

            var roleResult = await _userManager.AddToRoleAsync(user, "User");
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return roleResult;
            }

            return IdentityResult.Success;
        }
        public async Task<AuthSessionResult?> RefreshAsync(string refreshToken)
        {
            var rotated = await _refreshTokenService.RotateAsync(refreshToken);
            if (rotated == null)
                return null;

            var accessToken = await _jwtTokenService.GenerateTokenAsync(rotated.Value.User);

            return new AuthSessionResult
            {
                Access = accessToken,
                RefreshToken = rotated.Value.RawToken
            };
        }
        public async Task LogoutAsync(string refreshToken)
        {
            await _refreshTokenService.RevokeAsync(refreshToken);
        }
    }
}