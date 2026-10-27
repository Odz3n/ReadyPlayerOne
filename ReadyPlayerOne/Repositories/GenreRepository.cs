using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Repositories
{
    public class GenreRepository : IRepository<Genre, Guid>
    {
        private readonly ApplicationDbContext _context;

        public GenreRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Genre entity, CancellationToken token)
        {
            await _context.Genres.AddAsync(entity, token);
            await _context.SaveChangesAsync(token);
        }

        public async Task DeleteAsync(Guid id, CancellationToken token)
        {
            var genre = await _context.Genres
                .FirstOrDefaultAsync(g => g.Id == id, token);

            if (genre is null)
                return;

            _context.Genres.Remove(genre);
            await _context.SaveChangesAsync(token);
        }

        public IQueryable<Genre> GetAllAsQueryable()
        {
            return _context.Genres.AsNoTracking();
        }

        public async Task<IEnumerable<Genre>> GetAllAsync(
            CancellationToken token)
        {
            return await _context.Genres
                .AsNoTracking()
                .ToListAsync(token);
        }

        public async Task<Genre?> GetByIdAsync(
            Guid id,
            CancellationToken token)
        {
            return await _context.Genres
                .FirstOrDefaultAsync(g => g.Id == id, token);
        }

        public async Task UpdateAsync(
            Genre entity,
            Guid oldId,
            CancellationToken token)
        {
            var existing = await _context.Genres
                .FirstOrDefaultAsync(g => g.Id == oldId, token);

            if (existing is null)
                return;

            existing.Name = entity.Name;

            await _context.SaveChangesAsync(token);
        }
    }
}