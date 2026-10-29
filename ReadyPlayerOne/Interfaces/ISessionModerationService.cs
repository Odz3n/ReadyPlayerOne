using ReadyPlayerOne.DTOs.GameSessions;

namespace ReadyPlayerOne.Interfaces
{
    public interface ISessionModerationService
    {
        Task KickAsync(Guid sessionId, string targetUserId, string actorId, bool isAdmin, CancellationToken ct);
        Task BanAsync(Guid sessionId, string targetUserId, string actorId, bool isAdmin, string? reason, CancellationToken ct);
        Task UnbanAsync(Guid sessionId, string targetUserId, string actorId, bool isAdmin, CancellationToken ct);
        Task<List<SessionBanResponse>> GetSessionBansAsync(Guid sessionId, string actorId, bool isAdmin, CancellationToken ct);
    }
}
