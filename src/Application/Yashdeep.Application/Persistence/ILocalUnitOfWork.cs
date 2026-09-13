namespace Yashdeep.Application.Persistence;

public interface ILocalTransaction : IAsyncDisposable, IDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public interface ILocalUnitOfWork
{
    Task<ILocalTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
