using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Repositories;

public class GameRepository : IRepository<Game, Guid>
{
    private readonly ApplicationDbContext _context;

    public GameRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Game entity, CancellationToken token)
    {
        await _context.Games.AddAsync(entity, token);
        await _context.SaveChangesAsync(token);
    }

    public async Task DeleteAsync(Guid id, CancellationToken token)
    {
        var game = _context.Games.Local.FirstOrDefault(g => g.Id == id)
            ?? await _context.Games.FirstOrDefaultAsync(g => g.Id == id, token);

        if (game is null)
            return;

        _context.Games.Remove(game);
        await _context.SaveChangesAsync(token);
    }

    public IQueryable<Game> GetAllAsQueryable()
    {
        return _context.Games.AsNoTracking();
    }

    public async Task<IEnumerable<Game>> GetAllAsync(CancellationToken token)
    {
        return await _context.Games
            .AsNoTracking()
            .ToListAsync(token);
    }

    public async Task<Game?> GetByIdAsync(Guid id, CancellationToken token)
    {
        return await _context.Games
            .FirstOrDefaultAsync(g => g.Id == id, token);
    }

    public async Task UpdateAsync(Game entity, Guid oldId, CancellationToken token)
    {
        if (entity.Id != oldId)
            throw new ArgumentException("Game ID cannot be changed.");

        if (_context.Entry(entity).State == EntityState.Detached)
        {
            var existing = await _context.Games
                .FirstOrDefaultAsync(g => g.Id == oldId, token);

            if (existing is null)
                return;

            _context.Entry(existing).CurrentValues.SetValues(entity);
        }

        await _context.SaveChangesAsync(token);
    }
}