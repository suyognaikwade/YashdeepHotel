namespace Yashdeep.Application.Common.Interfaces;

/// <summary>
/// Generic repository persistence abstraction for local SQLite entities.
/// Supports atomic operations within an active Unit of Work / transaction.
/// </summary>
/// <typeparam name="TEntity">The domain or persistence entity type.</typeparam>
public interface ILocalRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Gets an IQueryable interface for querying entities.
    /// </summary>
    IQueryable<TEntity> Query();

    /// <summary>
    /// Finds an entity by primary key.
    /// </summary>
    Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all entities.
    /// </summary>
    Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity to the persistence tracking context.
    /// </summary>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a range of new entities to the persistence tracking context.
    /// </summary>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an existing entity as modified.
    /// </summary>
    void Update(TEntity entity);

    /// <summary>
    /// Marks an entity for deletion.
    /// </summary>
    void Remove(TEntity entity);
}
