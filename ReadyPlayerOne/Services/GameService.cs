using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Games;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;
using System.Text.RegularExpressions;

namespace ReadyPlayerOne.Services;

public class GameService : IGameService
{
    private readonly IRepository<Game, Guid> _gameRepository;
    private readonly IRepository<Genre, Guid> _genreRepository;

    public GameService(
        IRepository<Game, Guid> gameRepository,
        IRepository<Genre, Guid> genreRepository)
    {
        _gameRepository = gameRepository;
        _genreRepository = genreRepository;
    }

    public async Task<PagedResult<GameResponse>> GetAllAsync(
        GameQueryRequest request,
        CancellationToken token)
    {
        if (request.PageNumber < 1 || request.PageSize is < 1 or > 100)
            throw new ArgumentException("Invalid pagination parameters.");

        if (request.Search?.Length > 100)
            throw new ArgumentException("Search cannot exceed 100 characters.");

        IQueryable<Game> query = _gameRepository.GetAllAsQueryable();

        if (request.GenreId.HasValue)
            query = query.Where(g => g.GenreId == request.GenreId.Value);

        var search = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(g => g.Name.Contains(search));
        }

        int totalCount = await query.CountAsync(token);

        long offset = ((long)request.PageNumber - 1) * request.PageSize;

        var games = offset > int.MaxValue
            ? new List<GameResponse>()
            : await query
                .OrderBy(g => g.Name)
                .ThenBy(g => g.Id)
                .Skip((int)offset)
                .Take(request.PageSize)
                .Select(g => new GameResponse
                {
                    Id = g.Id,
                    Name = g.Name,
                    Slug = g.Slug,
                    Description = g.Description,
                    GenreId = g.GenreId,
                    Genre = g.Genre.Name,
                    CoverImageUrl = g.CoverImageUrl
                })
                .ToListAsync(token);

        return new PagedResult<GameResponse>
        {
            Items = games,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<GameResponse?> GetByIdAsync(
        Guid id,
        CancellationToken token)
    {
        return await _gameRepository.GetAllAsQueryable()
            .Where(g => g.Id == id)
            .Select(g => new GameResponse
            {
                Id = g.Id,
                Name = g.Name,
                Slug = g.Slug,
                Description = g.Description,
                GenreId = g.GenreId,
                Genre = g.Genre.Name,
                CoverImageUrl = g.CoverImageUrl
            })
            .FirstOrDefaultAsync(token);
    }

    public async Task<GameResponse> CreateAsync(
        CreateGameRequest request,
        CancellationToken token)
    {
        ValidateName(request.Name);
        ValidateDescription(request.Description);
        ValidateCoverImageUrl(request.CoverImageUrl);

        var name = request.Name.Trim();
        var slug = NormalizeSlug(request.Slug);

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

        try
        {
            await _gameRepository.AddAsync(game, token);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException(
                $"Game with slug '{slug}' already exists.", ex);
        }

        return new GameResponse
        {
            Id = game.Id,
            Name = game.Name,
            Slug = game.Slug,
            Description = game.Description,
            GenreId = genre.Id,
            Genre = genre.Name,
            CoverImageUrl = game.CoverImageUrl
        };
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateGameRequest request,
        CancellationToken token)
    {
        var game = await _gameRepository.GetByIdAsync(id, token);

        if (game is null)
            return false;

        ValidateName(request.Name);
        ValidateDescription(request.Description);
        ValidateCoverImageUrl(request.CoverImageUrl);

        var name = request.Name.Trim();
        var slug = NormalizeSlug(request.Slug);

        await ValidateGenreAsync(request.GenreId, token);
        await ValidateSlugUniquenessAsync(slug, id, token);

        game.Name = name;
        game.Slug = slug;
        game.Description = request.Description?.Trim();
        game.GenreId = request.GenreId;
        game.CoverImageUrl = request.CoverImageUrl?.Trim();

        try
        {
            await _gameRepository.UpdateAsync(game, id, token);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException(
                $"Game with slug '{slug}' already exists.", ex);
        }

        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken token)
    {
        var game = await _gameRepository.GetByIdAsync(id, token);

        if (game is null)
            return false;

        bool hasSessions = await _gameRepository.GetAllAsQueryable()
            .AnyAsync(g => g.Id == id && g.Sessions.Any(), token);

        if (hasSessions)
            throw new InvalidOperationException(
                "Cannot delete a game that has existing sessions.");

        try
        {
            await _gameRepository.DeleteAsync(id, token);
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            throw new InvalidOperationException(
                "Cannot delete a game that is referenced by other records.", ex);
        }

        return true;
    }

    private async Task<Genre> ValidateGenreAsync(
        Guid genreId,
        CancellationToken token)
    {
        if (genreId == Guid.Empty)
            throw new ArgumentException("Genre ID cannot be empty.");

        var genre = await _genreRepository.GetByIdAsync(genreId, token);

        return genre ?? throw new KeyNotFoundException(
            $"Genre with ID '{genreId}' not found.");
    }

    private async Task ValidateSlugUniquenessAsync(
        string slug,
        Guid? excludedGameId,
        CancellationToken token)
    {
        bool exists = await _gameRepository.GetAllAsQueryable()
            .AnyAsync(g =>
                g.Slug == slug &&
                (!excludedGameId.HasValue || g.Id != excludedGameId.Value),
                token);

        if (exists)
            throw new InvalidOperationException(
                $"Game with slug '{slug}' already exists.");
    }

    private static string NormalizeSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Game slug cannot be empty.");

        var normalized = slug.Trim().ToLowerInvariant();

        if (normalized.Length > 100)
            throw new ArgumentException(
                "Game slug cannot exceed 100 characters.");

        if (!Regex.IsMatch(
            normalized,
            @"^[a-z0-9]+(?:-[a-z0-9]+)*$"))
        {
            throw new ArgumentException(
                "Game slug can contain only letters, numbers and hyphens.");
        }

        return normalized;
    }

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Game name cannot be empty.");

        if (name.Trim().Length > 100)
            throw new ArgumentException(
                "Game name cannot exceed 100 characters.");
    }

    private static void ValidateDescription(string? description)
    {
        if (description?.Trim().Length > 1000)
            throw new ArgumentException(
                "Game description cannot exceed 1000 characters.");
    }

    private static void ValidateCoverImageUrl(string? coverImageUrl)
    {
        if (string.IsNullOrWhiteSpace(coverImageUrl))
            return;

        var url = coverImageUrl.Trim();

        if (url.Length > 2048)
            throw new ArgumentException(
                "Cover image URL cannot exceed 2048 characters.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Cover image URL must be a valid HTTP or HTTPS URL.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sql &&
               (sql.Number == 2601 || sql.Number == 2627);
    }

    private static bool IsForeignKeyViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sql && sql.Number == 547;
    }
}