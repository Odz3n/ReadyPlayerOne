using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Genres;

namespace ReadyPlayerOne.Interfaces
{
    public interface IGenreService
    {
        Task<PagedResult<GenreResponse>> GetAllAsync(PaginationRequest pagination, CancellationToken token);
        Task<GenreResponse?> GetByIdAsync(Guid id, CancellationToken token);
        Task<GenreResponse> CreateAsync(CreateGenreRequest request, CancellationToken token);
        Task<bool> UpdateAsync(Guid id, UpdateGenreRequest request, CancellationToken token);
        Task<bool> DeleteAsync(Guid id, CancellationToken token);
    }
}