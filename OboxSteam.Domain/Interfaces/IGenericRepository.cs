using System.Linq.Expressions;

namespace OboxSteam.Domain.Interfaces;

public interface IGenericRepository<TEntity> where TEntity : Entities.BaseEntity
{
    Task<List<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> predicate = null,
        params Expression<Func<TEntity, object>>[] includes);

    /// <summary>Returns rows including soft-deleted (bypasses global query filter).</summary>
    Task<List<TEntity>> GetAllIncludingDeletedAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        params Expression<Func<TEntity, object>>[] includes);

    Task<bool> AnyIncludingDeletedAsync(Expression<Func<TEntity, bool>>? predicate = null);

    Task<TEntity?> GetByIdAsync(Guid id, params Expression<Func<TEntity, object>>[] includes);

    Task<TEntity> AddAsync(TEntity entity);

    Task<bool> UpdateRange(List<TEntity> entities);

    Task<bool> Update(TEntity entity);

    Task<bool> SoftRemoveRangeById(List<Guid> entitiesId);

    Task<bool> SoftRemoveRange(List<TEntity> entities);

    Task<bool> SoftRemove(TEntity entity);

    Task AddRangeAsync(List<TEntity> entities);

    IQueryable<TEntity> GetQueryable();

    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate = null,
        params Expression<Func<TEntity, object>>[] includes);

    Task<bool> HardRemoveRange(List<TEntity> entities);

    Task<bool> HardRemove(Expression<Func<TEntity, bool>> predicate);
}
