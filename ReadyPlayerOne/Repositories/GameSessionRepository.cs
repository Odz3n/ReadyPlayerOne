
using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Repositories
{
    public class GameSessionRepository : IRepository<GameSession, Guid>
    {
        private readonly ApplicationDbContext _context;

        public GameSessionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(GameSession entity, CancellationToken token)
        {
            await _context.GameSessions.AddAsync(entity, token);
            await _context.SaveChangesAsync(token);
        }

        public async Task DeleteAsync(Guid id, CancellationToken token)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(token);

                await _context.SessionParticipants
                    .Where(p => p.GameSessionId == id)
                    .ExecuteDeleteAsync(token);

                await _context.Teams
                    .Where(t => t.GameSessionId == id)
                    .ExecuteDeleteAsync(token);

                await _context.GameSessions
                    .Where(s => s.Id == id)
                    .ExecuteDeleteAsync(token);

                await transaction.CommitAsync(token);
            });
        }

        public IQueryable<GameSession> GetAllAsQueryable()
        {
            return _context.GameSessions.AsNoTracking();
        }

        public async Task<IEnumerable<GameSession>> GetAllAsync(CancellationToken token)
        {
            return await _context.GameSessions.AsNoTracking().ToListAsync(token);
        }

        public async Task<GameSession?> GetByIdAsync(Guid id, CancellationToken token)
        {
            return await _context.GameSessions
                .Include(s => s.Game)
                .Include(s => s.Participants).ThenInclude(p => p.User)
                .Include(s => s.Teams)
                .FirstOrDefaultAsync(s => s.Id == id, token);
        }

        public async Task UpdateAsync(GameSession entity, Guid id, CancellationToken token)
        {
            var existing = await _context.GameSessions.FirstOrDefaultAsync(s => s.Id == id, token);
            if (existing is null)
                return;

            existing.Name = entity.Name;
            existing.Mode = entity.Mode;
            existing.Status = entity.Status;
            existing.MaxPlayers = entity.MaxPlayers;
            existing.RequiredTeams = entity.RequiredTeams;
            existing.PlayersPerTeam = entity.PlayersPerTeam;
            existing.StartedAt = entity.StartedAt;
            existing.EndedAt = entity.EndedAt;

            await _context.SaveChangesAsync(token);
        }
    }
}