using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Games;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Services
{
    public class GameService : IGameService
    {
        private readonly IRepository<Game, Guid> _gameRepository;
        public GameService(IRepository<Game, Guid> gameRepository)
        {
            _gameRepository = gameRepository;
        }

        public async Task<GameResponse> CreateAsync(CreateGameRequest request, CancellationToken token)
        {
            ValidateName(request.Name);

            var game = new Game
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = request.Description?.Trim()
            };

            await _gameRepository.AddAsync(game, token);

            return new GameResponse
            {
                Id = game.Id,
                Name = game.Name,
                Description = game.Description
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

        public async Task<PagedResult<GameResponse>> GetAllAsync(
            PaginationRequest pagination,
            CancellationToken token)
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
                    Description = g.Description
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
                    Description = g.Description
                })
                .FirstOrDefaultAsync(token);
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateGameRequest request, CancellationToken token)
        {
            ValidateName(request.Name);

            var game = await _gameRepository.GetByIdAsync(id, token);

            if (game is null)
                return false;

            game.Name = request.Name.Trim();
            game.Description = request.Description?.Trim();

            await _gameRepository.UpdateAsync(game, id, token);

            return true;
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Game name cannot be empty.");
        }
    }
}
