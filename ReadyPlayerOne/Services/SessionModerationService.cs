using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.DTOs.GameSessions;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;
using System.Data;

namespace ReadyPlayerOne.Services
{
    public class SessionAccessDeniedException: Exception
    {
        public SessionAccessDeniedException(string message)
            : base(message) { }
    }

    public class SessionModerationService : ISessionModerationService
    {
        private readonly ApplicationDbContext _db;
        public SessionModerationService(
            ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task BanAsync(Guid sessionId, string targetUserId, string actorId, bool isAdmin, string? reason, CancellationToken ct)
        {
            ValidateIds(sessionId, targetUserId);
            var reasonText = ValidateReason(reason);
            var banId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var session = await RequireSessionAsync(sessionId, ct);
                CheckPermission(session, actorId, isAdmin);
                EnsureWaiting(session);
                EnsureNotHost(session, targetUserId);
                if (!await _db.Users.AsNoTracking().AnyAsync(u => u.Id == targetUserId, ct))
                    throw new KeyNotFoundException("User not found.");

                bool exists = await _db.SessionBans.AsNoTracking()
                    .AnyAsync(b => b.GameSessionId == sessionId && b.UserId == targetUserId, ct);
                if (!exists)
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [SessionBans]
                        ([Id], [GameSessionId], [UserId], [BannedByUserId], [Reason], [CreatedAt])
                    VALUES ({banId}, {sessionId}, {targetUserId}, {actorId}, {reasonText}, {now});
                    """, ct);
                }

                await _db.SessionParticipants
                    .Where(p => p.GameSessionId == sessionId && p.UserId == targetUserId)
                    .ExecuteDeleteAsync(ct);
                await tx.CommitAsync(ct);
            });
        }

        public async Task<List<SessionBanResponse>> GetSessionBansAsync(Guid sessionId, string actorId, bool isAdmin, CancellationToken ct)
        {
            if (sessionId == Guid.Empty) 
                throw new ArgumentException("Session ID cannot be empty.");

            var session = await _db.GameSessions.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == sessionId, ct)
                ?? throw new KeyNotFoundException("Session not found.");

            CheckPermission(session, actorId, isAdmin);

            return await _db.SessionBans
                .AsNoTracking()
                .Where(b => b.GameSessionId == sessionId)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new SessionBanResponse
                {
                    UserId = b.UserId,
                    UserName = b.User.UserName ?? string.Empty,
                    BannedByUserId = b.BannedByUserId,
                    BannedByUserName = b.BannedByUser.UserName ?? string.Empty,
                    Reason = b.Reason,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync(ct);
        }

        public async Task KickAsync(Guid sessionId, string targetUserId, string actorId, bool isAdmin, CancellationToken ct)
        {
            ValidateIds(sessionId, targetUserId);
            bool removed = false;
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var session = await RequireSessionAsync(sessionId, ct);
                CheckPermission(session, actorId, isAdmin);
                EnsureWaiting(session);
                EnsureNotHost(session, targetUserId);

                int deleted = await _db.SessionParticipants
                    .Where(p => p.GameSessionId == sessionId && p.UserId == targetUserId)
                    .ExecuteDeleteAsync(ct);
                if (deleted == 0 && !removed)
                    throw new KeyNotFoundException("Participant not found in this session.");

                removed |= deleted == 1;
                await tx.CommitAsync(ct);
            });
        }

        public async Task UnbanAsync(Guid sessionId, string targetUserId, string actorId, bool isAdmin, CancellationToken ct)
        {
            ValidateIds(sessionId, targetUserId);
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var session = await RequireSessionAsync(sessionId, ct);
                CheckPermission(session, actorId, isAdmin);
                await _db.SessionBans
                    .Where(b => b.GameSessionId == sessionId && b.UserId == targetUserId)
                    .ExecuteDeleteAsync(ct);
                await tx.CommitAsync(ct);
            });
        }

        private async Task<GameSession> RequireSessionAsync(Guid sessionId, CancellationToken ct)
        {
            return await _db.GameSessions.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == sessionId, ct)
                ?? throw new KeyNotFoundException("Session not found.");
        }

        private static void CheckPermission(GameSession session, string actorId, bool isAdmin)
        {
            if (session.HostId != actorId && !isAdmin)
                throw new UnauthorizedAccessException("Only the host or an administrator can moderate this session.");
        }

        private static void EnsureWaiting(GameSession session)
        {
            if (session.Status != GameSessionStatus.Waiting)
                throw new InvalidOperationException("Session must be in Waiting status.");
        }

        private static void EnsureNotHost(GameSession session, string targetUserId)
        {
            if (session.HostId == targetUserId)
                throw new InvalidOperationException("The host cannot be kicked or banned from their own session.");
        }

        private static void ValidateIds(Guid sessionId, string targetUserId)
        {
            if (sessionId == Guid.Empty)
                throw new ArgumentException("Session ID cannot be empty.");
            if (string.IsNullOrWhiteSpace(targetUserId))
                throw new ArgumentException("User ID required.");
        }

        private static string? ValidateReason(string? reason)
        {
            var value = reason?.Trim();
            if (value?.Length > 500)
                throw new ArgumentException("Reason cannot exceed 500 characters.");

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
