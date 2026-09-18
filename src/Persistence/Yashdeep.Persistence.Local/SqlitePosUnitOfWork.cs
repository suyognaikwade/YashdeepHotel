using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Domain.Entities.Audit;
using Yashdeep.Domain.Entities.Billing;
using Yashdeep.Domain.Entities.Inventory;
using Yashdeep.Domain.Entities.Orders;
using Yashdeep.Domain.Entities.Sync;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Persistence.Local;

public class SqlitePosUnitOfWork : ILocalPosUnitOfWork, IOrderRepository, IBillRepository, IStockRepository, IAuditRepository, IPosOutboxRepository, IAsyncDisposable, IDisposable
{
    private readonly LocalPosDbContext _dbContext;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public IOrderRepository Orders => this;
    public IBillRepository Bills => this;
    public IStockRepository Stock => this;
    public IAuditRepository Audits => this;
    public IPosOutboxRepository Outbox => this;

    public SqlitePosUnitOfWork(LocalPosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // --- Order Repository ---
    async Task IOrderRepository.AddAsync(Order order, CancellationToken cancellationToken)
    {
        if (_dbContext.Entry(order).State == EntityState.Detached)
        {
            await _dbContext.Orders.AddAsync(order, cancellationToken);
        }
    }

    async Task<Order?> IOrderRepository.GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        return await _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.Kots)
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId, cancellationToken);
    }

    Task IOrderRepository.UpdateAsync(Order order, CancellationToken cancellationToken)
    {
        var entry = _dbContext.Entry(order);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.Orders.Update(order);
        }
        return Task.CompletedTask;
    }

    // --- Bill Repository ---
    async Task IBillRepository.AddAsync(Bill bill, CancellationToken cancellationToken)
    {
        if (_dbContext.Entry(bill).State == EntityState.Detached)
        {
            await _dbContext.Bills.AddAsync(bill, cancellationToken);
        }
    }

    async Task<Bill?> IBillRepository.GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        return await _dbContext.Bills
            .Include(b => b.Payments)
            .Include(b => b.TaxLines)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId, cancellationToken);
    }

    async Task<long> IBillRepository.GetNextDailySequenceNumberAsync(Guid tenantId, Guid branchId, DateOnly businessDate, CancellationToken cancellationToken)
    {
        long maxSeq = await _dbContext.Bills
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.BranchId == branchId && b.BusinessDate == businessDate)
            .MaxAsync(b => (long?)b.DailySequenceNumber, cancellationToken) ?? 0;

        return maxSeq + 1;
    }

    Task IBillRepository.UpdateAsync(Bill bill, CancellationToken cancellationToken)
    {
        var entry = _dbContext.Entry(bill);
        if (entry.State == EntityState.Detached)
        {
            _dbContext.Bills.Update(bill);
        }
        return Task.CompletedTask;
    }

    // --- Stock Repository ---
    async Task IStockRepository.AddMovementAsync(StockMovement movement, CancellationToken cancellationToken)
    {
        if (_dbContext.Entry(movement).State == EntityState.Detached)
        {
            await _dbContext.StockMovements.AddAsync(movement, cancellationToken);
        }
    }

    async Task<IReadOnlyList<StockMovement>> IStockRepository.GetMovementsByReferenceAsync(Guid referenceId, Guid tenantId, CancellationToken cancellationToken)
    {
        var list = await _dbContext.StockMovements
            .Where(s => s.ReferenceTransactionId == referenceId && s.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    // --- Audit Repository ---
    async Task IAuditRepository.AddEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        if (_dbContext.Entry(auditEvent).State == EntityState.Detached)
        {
            await _dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken);
        }
    }

    async Task<IReadOnlyList<AuditEvent>> IAuditRepository.GetEventsByReferenceAsync(Guid referenceId, Guid tenantId, CancellationToken cancellationToken)
    {
        var list = await _dbContext.AuditEvents
            .Where(a => a.ReferenceId == referenceId && a.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    // --- Outbox Repository ---
    async Task IPosOutboxRepository.AddMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (_dbContext.Entry(message).State == EntityState.Detached)
        {
            await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
        }
    }

    async Task<IReadOnlyList<OutboxMessage>> IPosOutboxRepository.GetPendingMessagesAsync(Guid tenantId, int batchSize, CancellationToken cancellationToken)
    {
        var list = await _dbContext.OutboxMessages
            .Where(m => m.TenantId == tenantId && m.Status == OutboxMessageStatus.Pending)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    async Task IPosOutboxRepository.MarkAsUploadedAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken)
    {
        var ids = eventIds.ToList();
        var messages = await _dbContext.OutboxMessages
            .Where(m => ids.Contains(m.EventId))
            .ToListAsync(cancellationToken);

        foreach (var msg in messages)
        {
            msg.MarkUploaded();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // --- Transaction Control ---
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
            throw new InvalidOperationException("A transaction is already active.");

        _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task<int> CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            int records = await _dbContext.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
            return records;
        }
        catch
        {
            await _transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _transaction?.Dispose();
            _dbContext.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
            }
            await _dbContext.DisposeAsync();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
