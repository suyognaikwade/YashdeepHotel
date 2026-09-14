using Yashdeep.Domain.Entities.Audit;
using Yashdeep.Domain.Entities.Billing;
using Yashdeep.Domain.Entities.Inventory;
using Yashdeep.Domain.Entities.Orders;
using Yashdeep.Domain.Entities.Sync;

namespace Yashdeep.Application.Common.Interfaces;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
}

public interface IBillRepository
{
    Task AddAsync(Bill bill, CancellationToken cancellationToken = default);
    Task<Bill?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<long> GetNextDailySequenceNumberAsync(Guid tenantId, Guid branchId, DateOnly businessDate, CancellationToken cancellationToken = default);
    Task UpdateAsync(Bill bill, CancellationToken cancellationToken = default);
}

public interface IStockRepository
{
    Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockMovement>> GetMovementsByReferenceAsync(Guid referenceId, Guid tenantId, CancellationToken cancellationToken = default);
}

public interface IAuditRepository
{
    Task AddEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> GetEventsByReferenceAsync(Guid referenceId, Guid tenantId, CancellationToken cancellationToken = default);
}

public interface IOutboxRepository
{
    Task AddMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(Guid tenantId, int batchSize = 50, CancellationToken cancellationToken = default);
    Task MarkAsUploadedAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default);
}

public interface ILocalPosUnitOfWork
{
    IOrderRepository Orders { get; }
    IBillRepository Bills { get; }
    IStockRepository Stock { get; }
    IAuditRepository Audits { get; }
    IOutboxRepository Outbox { get; }
    Task<int> CommitTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IPrinterService
{
    Task<bool> PrintKotAsync(KotRecord kot, CancellationToken cancellationToken = default);
    Task<bool> PrintReceiptAsync(Bill bill, CancellationToken cancellationToken = default);
}

public interface ICloudSyncEngine
{
    Task<bool> IsNetworkAvailableAsync(CancellationToken cancellationToken = default);
    Task<int> SynchronizePendingOutboxAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
