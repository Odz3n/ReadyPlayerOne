public interface IRepository<TEntity, TKey> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken token);
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken token);
    IQueryable<TEntity> GetAllAsQueryable();
    Task AddAsync(TEntity entity, CancellationToken token);
    Task UpdateAsync(TEntity entity, TKey id, CancellationToken token);
    Task DeleteAsync(TKey id, CancellationToken token);
}