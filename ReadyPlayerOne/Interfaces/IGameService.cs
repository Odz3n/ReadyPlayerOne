using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Games;

namespace ReadyPlayerOne.Interfaces;

public interface IGameService
{
    Task<PagedResult<GameResponse>> GetAllAsync(GameQueryRequest request, CancellationToken token);
    Task<GameResponse?> GetByIdAsync(Guid id, CancellationToken token);
    Task<GameResponse> CreateAsync(CreateGameRequest request, CancellationToken token);
    Task<bool> UpdateAsync(Guid id, UpdateGameRequest request, CancellationToken token);
    Task<bool> DeleteAsync(Guid id, CancellationToken token);
}