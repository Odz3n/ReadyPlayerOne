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
        private readonly IServiceScopeFactory _scopeFactory;
        public RefreshTokenService(
            ApplicationDbContext context,
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _scopeFactory = scopeFactory;
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

            // Generate once so retries reuse the same token and identity.
            var newRawToken = GenerateToken();
            var newTokenHash = HashToken(newRawToken);
            var newTokenId = Guid.NewGuid();

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync<(string RawToken, ApplicationUser User)?>(async () =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                await using var transaction = await db.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable);

                // Lock the old token during validation and rotation.
                var oldToken = await db.RefreshTokens
                    .FromSqlInterpolated(
                        $"SELECT * FROM [RefreshTokens] WITH (UPDLOCK, HOLDLOCK) WHERE [TokenHash] = {hash}")
                    .Include(x => x.User)
                    .SingleOrDefaultAsync();

                if (oldToken is null)
                    return null;

                if (oldToken.RevokedAt != null)
                {
                    // Our previous attempt may have committed successfully
                    // but lost the connection during commit.
                    if (oldToken.ReplacedByTokenId == newTokenId)
                    {
                        bool committed = await db.RefreshTokens.AnyAsync(
                            x => x.Id == newTokenId &&
                                 x.TokenHash == newTokenHash);

                        if (!committed)
                            throw new InvalidOperationException(
                                "Inconsistent refresh token rotation state.");

                        return (newRawToken, oldToken.User);
                    }

                    // Another rotation already replaced this token.
                    // Preserve the existing reuse-detection policy.
                    if (oldToken.ReplacedByTokenId != null)
                    {
                        await db.RefreshTokens
                            .Where(x => x.FamilyId == oldToken.FamilyId &&
                                        x.RevokedAt == null)
                            .ExecuteUpdateAsync(setters =>
                                setters.SetProperty(x => x.RevokedAt, now));

                        await transaction.CommitAsync();
                    }

                    return null;
                }

                if (oldToken.ExpiresAt <= now)
                    return null;

                var newToken = new RefreshToken
                {
                    Id = newTokenId,
                    UserId = oldToken.UserId,
                    FamilyId = oldToken.FamilyId,
                    TokenHash = newTokenHash,
                    ExpiresAt = now.AddDays(7)
                };

                oldToken.RevokedAt = now;
                oldToken.ReplacedByTokenId = newToken.Id;

                db.RefreshTokens.Add(newToken);

                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                return (newRawToken, oldToken.User);
            });
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