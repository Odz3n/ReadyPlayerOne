using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
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
            var session = await _context.GameSessions
                .FirstOrDefaultAsync(gs => gs.Id == id, token);

            if (session is null)
                return;

            _context.GameSessions.Remove(session);
            await _context.SaveChangesAsync(token);
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
            return await _context.GameSessions.FirstOrDefaultAsync(gs => gs.Id == id, token);
        }

        public async Task UpdateAsync(GameSession entity, Guid id, CancellationToken token)
        {
            var existing = await _context.GameSessions
                .FirstOrDefaultAsync(gs => gs.Id == id, token);

            if (existing is null)
                return;

            existing.Name = entity.Name;
            existing.Mode = entity.Mode;
            existing.Status = entity.Status;
            existing.MaxPlayers = entity.MaxPlayers;
            existing.RequiredTeams = entity.RequiredTeams;
            existing.PlayersPerTeam = entity.PlayersPerTeam;

            await _context.SaveChangesAsync(token);
        }
    }
}
