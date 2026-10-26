using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ReadyPlayerOne.DTOs.Auth;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ReadyPlayerOne.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        public JwtTokenService(
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager)
        {
            _configuration = configuration;
            _userManager = userManager;
        }
        public async Task<AuthResponse> GenerateTokenAsync(ApplicationUser user)
        {
            var settings = _configuration.GetSection("Jwt");

            var key = settings["Key"]
                ?? throw new InvalidOperationException("JWT key missing.");

            var issuer = settings["Issuer"]
                ?? throw new InvalidOperationException("JWT issuer missing.");

            var audience = settings["Audience"]
                ?? throw new InvalidOperationException("JWT audience missing.");

            var expiresAt = DateTime.UtcNow.AddMinutes(10);

            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? "")
            };

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return new AuthResponse
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAt = expiresAt
            };
        }
    }
}
