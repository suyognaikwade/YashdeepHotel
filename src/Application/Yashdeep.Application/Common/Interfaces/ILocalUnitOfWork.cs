using System.Data;

namespace Yashdeep.Application.Common.Interfaces;

/// <summary>
/// Unit of Work abstraction encapsulating transaction boundaries for atomic local business operations.
/// Supports atomic commits, rollbacks, savepoints, and future Outbox pattern enqueuing.
/// </summary>
public interface ILocalUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets a repository for the specified entity type.
    /// </summary>
    ILocalRepository<TEntity> GetRepository<TEntity>() where TEntity : class;

    /// <summary>
    /// Begins an explicit database transaction with the specified isolation level.
    /// </summary>
    Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the active transaction and flushes changes to the local SQLite storage.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the active transaction and reverts uncommitted mutations.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves pending changes to the underlying DbContext without necessarily committing an outer transaction.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a transaction savepoint for nested or partial transaction rollbacks.
    /// </summary>
    Task CreateSavepointAsync(string savepointName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the active transaction to a previously created savepoint.
    /// </summary>
    Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether an explicit transaction is currently active.
    /// </summary>
    bool HasActiveTransaction { get; }
}
