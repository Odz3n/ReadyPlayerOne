using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Repositories
{
    public class SessionParticipantRepository : IRepository<SessionParticipant, Guid>
    {
        private readonly ApplicationDbContext _context;
        public SessionParticipantRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(SessionParticipant entity, CancellationToken token)
        {
            await _context.SessionParticipants.AddAsync(entity, token);
            await _context.SaveChangesAsync(token);
        }

        public async Task DeleteAsync(Guid id, CancellationToken token)
        {
            var participant = await _context.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.Id == id, token);

            if (participant is null)
                return;

            _context.SessionParticipants.Remove(participant);
            await _context.SaveChangesAsync(token);
        }

        public IQueryable<SessionParticipant> GetAllAsQueryable()
        {
            return _context.SessionParticipants.AsNoTracking();
        }

        public async Task<IEnumerable<SessionParticipant>> GetAllAsync(CancellationToken token)
        {
            return await _context.SessionParticipants.AsNoTracking().ToListAsync(token);
        }

        public async Task<SessionParticipant?> GetByIdAsync(Guid id, CancellationToken token)
        {
            return await _context.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.Id == id, token);
        }

        public async Task UpdateAsync(SessionParticipant entity, Guid id, CancellationToken token)
        {
            var existing = await _context.SessionParticipants
                .FirstOrDefaultAsync(sp => sp.Id == id, token);

            if (existing is null)
                return;

            existing.TeamId = entity.TeamId;
            existing.IsReady = entity.IsReady;

            await _context.SaveChangesAsync(token);
        }
    }
}
