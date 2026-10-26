using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;
using System.Security.Cryptography;
using System.Text;
using System.Data;

namespace ReadyPlayerOne.Services
{
    public class RefreshTokenService: IRefreshTokenService
    {
        private readonly ApplicationDbContext _context;
        public RefreshTokenService(
            ApplicationDbContext context)
        {
            _context = context;
        }
        private static string GenerateToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }
        private static string HashToken(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }
        public async Task<(string RawToken, RefreshToken Entity)> CreateAsync(ApplicationUser user)
        {
            var rawToken = GenerateToken();
            var entity = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashToken(rawToken),
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            _context.RefreshTokens.Add(entity);
            await _context.SaveChangesAsync();

            return (rawToken, entity);
        }
        public async Task<(string RawToken, ApplicationUser User)?> RotateAsync(string rawToken)
        {
            var hash = HashToken(rawToken);
            var now = DateTime.UtcNow;

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var oldToken = await _context.RefreshTokens
                .Include(x => x.User)
                .SingleOrDefaultAsync(x => x.TokenHash == hash);

            if (oldToken == null)
                return null;

            if (oldToken.RevokedAt != null)
            {
                if (oldToken.ReplacedByTokenId != null)
                {
                    await _context.RefreshTokens
                        .Where(x => x.FamilyId == oldToken.FamilyId && x.RevokedAt == null)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, now));

                    await transaction.CommitAsync();
                }

                return null;
            }

            if (oldToken.ExpiresAt <= now)
                return null;

            var newRawToken = GenerateToken();
            var newToken = new RefreshToken
            {
                UserId = oldToken.UserId,
                FamilyId = oldToken.FamilyId,
                TokenHash = HashToken(newRawToken),
                ExpiresAt = now.AddDays(7)
            };

            oldToken.RevokedAt = now;
            oldToken.ReplacedByTokenId = newToken.Id;

            _context.RefreshTokens.Add(newToken);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return (newRawToken, oldToken.User);
        }
        public async Task RevokeAsync(string rawToken)
        {
            var hash = HashToken(rawToken);

            await _context.RefreshTokens
                .Where(x => x.TokenHash == hash &&
                            x.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.RevokedAt, DateTime.UtcNow));
        }
    }
}
