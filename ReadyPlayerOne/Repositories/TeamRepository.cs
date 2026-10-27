using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Repositories
{
    public class TeamRepository : IRepository<Team, Guid>
    {
        private readonly ApplicationDbContext _context;
        public TeamRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Team entity, CancellationToken token)
        {
            await _context.Teams.AddAsync(entity, token);
            await _context.SaveChangesAsync(token);
        }

        public async Task DeleteAsync(Guid id, CancellationToken token)
        {
            var team = await _context.Teams
                .FirstOrDefaultAsync(t => t.Id == id, token);
            if (team == null)
                return;

            await using var transaction = await _context.Database.BeginTransactionAsync(token);

            await _context.SessionParticipants
                .Where(p => p.TeamId == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.TeamId, (Guid?)null), token);

            _context.Teams.Remove(team);
            await _context.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }

        public IQueryable<Team> GetAllAsQueryable()
        {
            return _context.Teams.AsNoTracking();
        }

        public async Task<IEnumerable<Team>> GetAllAsync(CancellationToken token)
        {
            return await _context.Teams.AsNoTracking().ToListAsync(token);
        }

        public async Task<Team?> GetByIdAsync(Guid id, CancellationToken token)
        {
            return await _context.Teams.FirstOrDefaultAsync(t => t.Id == id, token);
        }

        public async Task UpdateAsync(Team entity, Guid id, CancellationToken token)
        {
            var existing = await _context.Teams
                .FirstOrDefaultAsync(t => t.Id == id, token);
            if (existing is null)
                return;

            existing.Name = entity.Name;

            await _context.SaveChangesAsync(token);
        }
    }
}
