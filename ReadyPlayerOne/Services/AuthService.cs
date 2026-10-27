using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using ReadyPlayerOne.DTOs.Auth;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;
using System.Text;
using System.Text.Encodings.Web;

namespace ReadyPlayerOne.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        public AuthService(
            UserManager<ApplicationUser> userManager,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService refreshTokenService,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _jwtTokenService = jwtTokenService;
            _refreshTokenService = refreshTokenService;
            _configuration = configuration;
            _emailService = emailService;
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

            if (!await _userManager.IsEmailConfirmedAsync(user))
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

            // Email confirmation
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var frontendUrl = _configuration["Client:BaseUrl"]
                ?? throw new InvalidOperationException("Frontend URL is not configured.");

            var confirmationUrl =
                $"{frontendUrl.TrimEnd('/')}/confirm-email" +
                $"?userId={Uri.EscapeDataString(user.Id)}" +
                $"&token={Uri.EscapeDataString(encodedToken)}";

            await _emailService.SendEmailAsync(user.Email!, "Confirm your email",
                $"""
                <h2>Welcome to ReadyPlayerOne!</h2>
                
                <p>Please confirm your email address to activate your account.</p>
                <a href="{HtmlEncoder.Default.Encode(confirmationUrl)}">
                    Confirm Email
                </a>
                """, CancellationToken.None);

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

        public async Task<IdentityResult> ConfirmEmailAsync(string userId, string encodedToken)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                return IdentityResult.Failed(
                    new IdentityError { Description = "User not found." });

            var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));

            return await _userManager.ConfirmEmailAsync(user, token);
        }

        public async Task ResendConfirmationAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || user.EmailConfirmed)
                return;

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var encodedToken = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));

            var frontendUrl = _configuration["Client:BaseUrl"]
                ?? throw new InvalidOperationException("Frontend URL is not configured.");

            var confirmationUrl =
                $"{frontendUrl.TrimEnd('/')}/confirm-email" +
                $"?userId={Uri.EscapeDataString(user.Id)}" +
                $"&token={Uri.EscapeDataString(encodedToken)}";

            await _emailService.SendEmailAsync(
                user.Email!,
                "Confirm your email",
                $"<a href=\"{HtmlEncoder.Default.Encode(confirmationUrl)}\">Confirm Email</a>",
                CancellationToken.None);
        }
    }
}