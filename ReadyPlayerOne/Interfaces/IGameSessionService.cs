using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.GameSessions;

namespace ReadyPlayerOne.Interfaces
{
    public interface IGameSessionService
    {
        Task<PagedResult<GameSessionResponse>> GetAllAsync(GameSessionQueryRequest request, CancellationToken token);
        Task<GameSessionResponse?> GetByIdAsync(Guid id, CancellationToken token);

        Task<GameSessionResponse> CreateAsync(CreateGameSessionRequest request, string userId, CancellationToken token);
        Task<bool> UpdateAsync(Guid id, UpdateGameSessionRequest request, string userId, bool isAdmin, CancellationToken token);
        Task<bool> DeleteAsync(Guid id, string userId, bool isAdmin, CancellationToken token);

        Task JoinAsync(Guid id, string userId, CancellationToken token);
        Task LeaveAsync(Guid id, string userId, CancellationToken token);
        Task SetReadyAsync(Guid id, string userId, bool isReady, CancellationToken token);

        Task<TeamResponse> CreateTeamAsync(Guid id, CreateTeamRequest request, string userId, bool isAdmin, CancellationToken token);
        Task JoinTeamAsync(Guid id, Guid teamId, string userId, CancellationToken token);
        Task LeaveTeamAsync(Guid id, string userId, CancellationToken token);

        Task StartAsync(Guid id, string userId, bool isAdmin, CancellationToken token);
        Task CompleteAsync(Guid id, string userId, bool isAdmin, CancellationToken token);
        Task CancelAsync(Guid id, string userId, bool isAdmin, CancellationToken token);

        Task DeleteTeamAsync(Guid sessionId, Guid teamId, string userId, bool isAdmin, CancellationToken token);

    }
}