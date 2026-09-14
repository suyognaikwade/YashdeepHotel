using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Application.Persistence;

namespace Yashdeep.Persistence.Local;

public class LocalTransaction : ILocalTransaction
{
    private readonly IDbContextTransaction _efTransaction;

    public LocalTransaction(IDbContextTransaction efTransaction)
    {
        _efTransaction = efTransaction ?? throw new ArgumentNullException(nameof(efTransaction));
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _efTransaction.CommitAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await _efTransaction.RollbackAsync(cancellationToken);
    }

    public void Dispose()
    {
        _efTransaction.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _efTransaction.DisposeAsync();
    }
}

/// <summary>
/// Unit of Work implementation supporting LocalDbContext and LocalPosDbContext for explicit local SQLite transactions.
/// </summary>
public class LocalUnitOfWork : Yashdeep.Application.Common.Interfaces.ILocalUnitOfWork, Yashdeep.Application.Persistence.ILocalUnitOfWork
{
    private readonly DbContext _dbContext;
    private readonly LocalDbContext? _localDbContext;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    public LocalUnitOfWork(LocalDbContext dbContext)
    {
        _localDbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dbContext = dbContext;
    }

    public LocalUnitOfWork(LocalPosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _localDbContext = null;
    }

    public bool HasActiveTransaction => _currentTransaction != null;

    public ILocalRepository<TEntity> GetRepository<TEntity>() where TEntity : class
    {
        if (_localDbContext == null)
        {
            throw new InvalidOperationException("GetRepository requires LocalDbContext.");
        }

        var type = typeof(TEntity);
        if (!_repositories.TryGetValue(type, out var repository))
        {
            repository = new LocalRepository<TEntity>(_localDbContext);
            _repositories[type] = repository;
        }

        return (ILocalRepository<TEntity>)repository;
    }

    public async Task<ILocalTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already active on this Unit of Work context.");
        }

        _currentTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new LocalTransaction(_currentTransaction);
    }

    public async Task BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already active on this Unit of Work context.");
        }

        _currentTransaction = await _dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    async Task Yashdeep.Application.Common.Interfaces.ILocalUnitOfWork.BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        await BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction is currently active to commit.");
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateSavepointAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("Cannot create a savepoint without an active transaction.");
        }

        if (string.IsNullOrWhiteSpace(savepointName))
        {
            throw new ArgumentException("Savepoint name cannot be null or empty.", nameof(savepointName));
        }

        await _currentTransaction.CreateSavepointAsync(savepointName, cancellationToken);
    }

    public async Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("Cannot rollback to savepoint without an active transaction.");
        }

        if (string.IsNullOrWhiteSpace(savepointName))
        {
            throw new ArgumentException("Savepoint name cannot be null or empty.", nameof(savepointName));
        }

        await _currentTransaction.RollbackToSavepointAsync(savepointName, cancellationToken);
    }

    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _currentTransaction?.Dispose();
                _dbContext.Dispose();
            }
            _disposed = true;
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        await _dbContext.DisposeAsync();
    }
}
