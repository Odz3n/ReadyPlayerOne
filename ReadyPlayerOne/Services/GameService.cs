using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Games;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;
using System.Text.RegularExpressions;

namespace ReadyPlayerOne.Services
{
    public class GameService : IGameService
    {
        private readonly IRepository<Game, Guid> _gameRepository;
        private readonly IRepository<Genre, Guid> _genreRepository;

        public GameService(IRepository<Game, Guid> gameRepository, IRepository<Genre, Guid> genreRepository)
        {
            _gameRepository = gameRepository;
            _genreRepository = genreRepository;
        }

        public async Task<GameResponse> CreateAsync(CreateGameRequest request, CancellationToken token)
        {
            ValidateName(request.Name);
            ValidateSlug(request.Slug);
            ValidateDescription(request.Description);
            ValidateCoverImageUrl(request.CoverImageUrl);

            var name = request.Name.Trim();
            var slug = request.Slug.Trim().ToLowerInvariant();

            var genre = await ValidateGenreAsync(request.GenreId, token);
            await ValidateSlugUniquenessAsync(slug, null, token);

            var game = new Game
            {
                Id = Guid.NewGuid(),
                Name = name,
                Slug = slug,
                Description = request.Description?.Trim(),
                GenreId = genre.Id,
                CoverImageUrl = request.CoverImageUrl?.Trim()
            };

            await _gameRepository.AddAsync(game, token);

            return new GameResponse
            {
                Id = game.Id,
                Name = game.Name,
                Slug = game.Slug,
                Description = game.Description,
                Genre = genre.Name,
                CoverImageUrl = game.CoverImageUrl
            };
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken token)
        {
            var game = await _gameRepository.GetByIdAsync(id, token);
            if (game is null)
                return false;

            bool hasSessions = await _gameRepository.GetAllAsQueryable()
                .AnyAsync(g => g.Id == id && g.Sessions.Any(), token);

            if (hasSessions)
                throw new InvalidOperationException("Cannot delete a game that has existing sessions.");

            await _gameRepository.DeleteAsync(id, token);
            return true;
        }

        public async Task<PagedResult<GameResponse>> GetAllAsync(PaginationRequest pagination, CancellationToken token)
        {
            var query = _gameRepository.GetAllAsQueryable();
            int totalCount = await query.CountAsync(token);

            var games = await query
                .OrderBy(g => g.Name)
                .ThenBy(g => g.Id)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .Select(g => new GameResponse
                {
                    Id = g.Id,
                    Name = g.Name,
                    Slug = g.Slug,
                    Description = g.Description,
                    Genre = g.Genre.Name,
                    CoverImageUrl = g.CoverImageUrl
                })
                .ToListAsync(token);

            return new PagedResult<GameResponse>
            {
                Items = games,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<GameResponse?> GetByIdAsync(Guid id, CancellationToken token)
        {
            return await _gameRepository.GetAllAsQueryable()
                .Where(g => g.Id == id)
                .Select(g => new GameResponse
                {
                    Id = g.Id,
                    Name = g.Name,
                    Slug = g.Slug,
                    Description = g.Description,
                    Genre = g.Genre.Name,
                    CoverImageUrl = g.CoverImageUrl
                })
                .FirstOrDefaultAsync(token);
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateGameRequest request, CancellationToken token)
        {
            var game = await _gameRepository.GetByIdAsync(id, token);
            if (game is null)
                return false;

            ValidateName(request.Name);
            ValidateSlug(request.Slug);
            ValidateDescription(request.Description);
            ValidateCoverImageUrl(request.CoverImageUrl);

            var name = request.Name.Trim();
            var slug = request.Slug.Trim().ToLowerInvariant();

            await ValidateGenreAsync(request.GenreId, token);
            await ValidateSlugUniquenessAsync(slug, id, token);

            game.Name = name;
            game.Slug = slug;
            game.Description = request.Description?.Trim();
            game.GenreId = request.GenreId;
            game.CoverImageUrl = request.CoverImageUrl?.Trim();

            await _gameRepository.UpdateAsync(game, id, token);
            return true;
        }

        private async Task<Genre> ValidateGenreAsync(Guid genreId, CancellationToken token)
        {
            if (genreId == Guid.Empty)
                throw new ArgumentException("Genre ID cannot be empty.");

            var genre = await _genreRepository.GetByIdAsync(genreId, token);
            if (genre is null)
                throw new KeyNotFoundException($"Genre with ID '{genreId}' not found.");

            return genre;
        }

        private async Task ValidateSlugUniquenessAsync(string slug, Guid? excludedGameId, CancellationToken token)
        {
            bool exists = await _gameRepository.GetAllAsQueryable()
                .AnyAsync(g => g.Slug == slug && (!excludedGameId.HasValue || g.Id != excludedGameId.Value), token);

            if (exists)
                throw new InvalidOperationException($"Game with slug '{slug}' already exists.");
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Game name cannot be empty.");

            if (name.Trim().Length > 100)
                throw new ArgumentException("Game name cannot exceed 100 characters.");
        }

        private static void ValidateSlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                throw new ArgumentException("Game slug cannot be empty.");

            var normalizedSlug = slug.Trim();

            if (normalizedSlug.Length > 100)
                throw new ArgumentException("Game slug cannot exceed 100 characters.");

            if (!Regex.IsMatch(normalizedSlug, @"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.IgnoreCase))
                throw new ArgumentException("Game slug can contain only letters, numbers and hyphens.");
        }

        private static void ValidateDescription(string? description)
        {
            if (description?.Trim().Length > 1000)
                throw new ArgumentException("Game description cannot exceed 1000 characters.");
        }

        private static void ValidateCoverImageUrl(string? coverImageUrl)
        {
            if (string.IsNullOrWhiteSpace(coverImageUrl))
                return;

            var url = coverImageUrl.Trim();

            if (url.Length > 2048)
                throw new ArgumentException("Cover image URL cannot exceed 2048 characters.");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("Cover image URL must be a valid HTTP or HTTPS URL.");
        }
    }
}