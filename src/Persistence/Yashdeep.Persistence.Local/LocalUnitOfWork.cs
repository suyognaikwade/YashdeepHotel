using Microsoft.EntityFrameworkCore.Storage;
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

public class LocalUnitOfWork : ILocalUnitOfWork
{
    private readonly LocalPosDbContext _dbContext;

    public LocalUnitOfWork(LocalPosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ILocalTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var efTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new LocalTransaction(efTx);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
