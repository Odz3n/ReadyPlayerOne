using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Genres;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Services
{
    public class GenreService : IGenreService
    {
        private readonly IRepository<Genre, Guid> _genreRepository;
        private readonly IRepository<Game, Guid> _gameRepository;

        public GenreService(
            IRepository<Genre, Guid> genreRepository,
            IRepository<Game, Guid> gameRepository)
        {
            _genreRepository = genreRepository;
            _gameRepository = gameRepository;
        }

        public async Task<GenreResponse> CreateAsync(CreateGenreRequest request, CancellationToken token)
        {
            var name = ValidateName(request.Name);

            await ValidateNameUniquenessAsync(name, null, token);

            var genre = new Genre
            {
                Id = Guid.NewGuid(),
                Name = name
            };

            await _genreRepository.AddAsync(genre, token);

            return new GenreResponse
            {
                Id = genre.Id,
                Name = genre.Name
            };
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken token)
        {
            var genre = await _genreRepository.GetByIdAsync(id, token);

            if (genre is null)
                return false;

            bool hasGames = await _gameRepository
                .GetAllAsQueryable()
                .AnyAsync(g => g.GenreId == id, token);

            if (hasGames)
                throw new InvalidOperationException(
                    "Cannot delete a genre that has associated games.");

            await _genreRepository.DeleteAsync(id, token);

            return true;
        }

        public async Task<PagedResult<GenreResponse>> GetAllAsync(PaginationRequest pagination, CancellationToken token)
        {
            var query = _genreRepository
                .GetAllAsQueryable()
                .AsNoTracking();

            int totalCount = await query.CountAsync(token);

            var genres = await query
                .OrderBy(g => g.Name)
                .ThenBy(g => g.Id)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .Select(g => new GenreResponse
                {
                    Id = g.Id,
                    Name = g.Name
                })
                .ToListAsync(token);

            return new PagedResult<GenreResponse>
            {
                Items = genres,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<GenreResponse?> GetByIdAsync(Guid id, CancellationToken token)
        {
            return await _genreRepository
                .GetAllAsQueryable()
                .AsNoTracking()
                .Where(g => g.Id == id)
                .Select(g => new GenreResponse
                {
                    Id = g.Id,
                    Name = g.Name
                })
                .FirstOrDefaultAsync(token);
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateGenreRequest request, CancellationToken token)
        {
            var genre = await _genreRepository.GetByIdAsync(id, token);

            if (genre is null)
                return false;

            var name = ValidateName(request.Name);

            await ValidateNameUniquenessAsync(name, id, token);

            genre.Name = name;

            await _genreRepository.UpdateAsync(genre, id, token);

            return true;
        }

        private async Task ValidateNameUniquenessAsync(string name, Guid? excludedGenreId, CancellationToken token)
        {
            var normalizedName = name.ToUpperInvariant();

            bool exists = await _genreRepository
                .GetAllAsQueryable()
                .AnyAsync(g =>
                    g.Name.ToUpper() == normalizedName &&
                    (!excludedGenreId.HasValue ||
                     g.Id != excludedGenreId.Value),
                    token);

            if (exists)
                throw new InvalidOperationException($"Genre '{name}' already exists.");
        }

        private static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Genre name cannot be empty.");

            var trimmedName = name.Trim();

            if (trimmedName.Length > 100)
                throw new ArgumentException("Genre name cannot exceed 100 characters.");

            return trimmedName;
        }
    }
}