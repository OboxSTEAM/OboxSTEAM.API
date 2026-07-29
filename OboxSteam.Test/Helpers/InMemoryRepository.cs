using System.Linq.Expressions;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Test.Helpers;

/// <summary>
/// In-memory <see cref="IGenericRepository{TEntity}"/> that evaluates predicates against a list store.
/// Suitable for Application-layer unit tests that rely on Expression filters.
/// </summary>
public sealed class InMemoryRepository<TEntity> : IGenericRepository<TEntity>
    where TEntity : BaseEntity
{
    private readonly List<TEntity> _items = [];

    public IReadOnlyList<TEntity> Items => _items;

    public void Seed(params TEntity[] entities)
    {
        _items.AddRange(entities);
    }

    public Task<List<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>> predicate = null!,
        params Expression<Func<TEntity, object>>[] includes)
    {
        IEnumerable<TEntity> query = _items;
        if (predicate != null)
            query = query.Where(predicate.Compile());

        return Task.FromResult(query.ToList());
    }

    public Task<List<TEntity>> GetAllIncludingDeletedAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        params Expression<Func<TEntity, object>>[] includes)
        => GetAllAsync(predicate!, includes);

    public Task<bool> AnyIncludingDeletedAsync(Expression<Func<TEntity, bool>>? predicate = null)
    {
        if (predicate == null)
            return Task.FromResult(_items.Count > 0);

        return Task.FromResult(_items.Any(predicate.Compile()));
    }

    public Task<TEntity?> GetByIdAsync(Guid id, params Expression<Func<TEntity, object>>[] includes)
        => Task.FromResult(_items.FirstOrDefault(e => e.Id == id));

    public Task<TEntity> AddAsync(TEntity entity)
    {
        _items.Add(entity);
        return Task.FromResult(entity);
    }

    public Task AddRangeAsync(List<TEntity> entities)
    {
        _items.AddRange(entities);
        return Task.CompletedTask;
    }

    public Task<bool> Update(TEntity entity) => Task.FromResult(true);

    public Task<bool> UpdateRange(List<TEntity> entities) => Task.FromResult(true);

    public Task<bool> SoftRemove(TEntity entity)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        return Task.FromResult(true);
    }

    public Task<bool> SoftRemoveRange(List<TEntity> entities)
    {
        foreach (var entity in entities)
            SoftRemove(entity);

        return Task.FromResult(true);
    }

    public Task<bool> SoftRemoveRangeById(List<Guid> entitiesId)
    {
        foreach (var id in entitiesId)
        {
            var entity = _items.FirstOrDefault(e => e.Id == id);
            if (entity != null)
                SoftRemove(entity);
        }

        return Task.FromResult(true);
    }

    public IQueryable<TEntity> GetQueryable() => _items.AsQueryable();

    public Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate = null!,
        params Expression<Func<TEntity, object>>[] includes)
    {
        if (predicate == null)
            return Task.FromResult(_items.FirstOrDefault());

        return Task.FromResult(_items.FirstOrDefault(predicate.Compile()));
    }

    public Task<bool> HardRemoveRange(List<TEntity> entities)
    {
        foreach (var entity in entities)
            _items.Remove(entity);

        return Task.FromResult(true);
    }

    public Task<bool> HardRemove(Expression<Func<TEntity, bool>> predicate)
    {
        var toRemove = _items.Where(predicate.Compile()).ToList();
        foreach (var entity in toRemove)
            _items.Remove(entity);

        return Task.FromResult(true);
    }
}
