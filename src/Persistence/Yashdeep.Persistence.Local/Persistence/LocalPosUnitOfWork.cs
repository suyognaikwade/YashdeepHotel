using System.Collections.Concurrent;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Domain.Entities.Audit;
using Yashdeep.Domain.Entities.Billing;
using Yashdeep.Domain.Entities.Inventory;
using Yashdeep.Domain.Entities.Orders;
using Yashdeep.Domain.Outbox;

namespace Yashdeep.Persistence.Local.Persistence;

public class LocalPosMemoryDbContext
{
    public ConcurrentDictionary<Guid, Order> Orders { get; } = new();
    public ConcurrentDictionary<Guid, Bill> Bills { get; } = new();
    public ConcurrentDictionary<Guid, StockMovement> StockMovements { get; } = new();
    public ConcurrentDictionary<Guid, AuditEvent> AuditEvents { get; } = new();
    public ConcurrentDictionary<Guid, OutboxMessage> OutboxMessages { get; } = new();
    private readonly ConcurrentDictionary<string, long> _dailyCounters = new();

    public long GetNextDailySequence(Guid tenantId, Guid branchId, DateOnly businessDate)
    {
        string key = $"{tenantId}_{branchId}_{businessDate:yyyyMMdd}";
        return _dailyCounters.AddOrUpdate(key, 1, (_, current) => current + 1);
    }
}

public class LocalPosUnitOfWork : ILocalPosUnitOfWork, IOrderRepository, IBillRepository, IStockRepository, IAuditRepository, IPosOutboxRepository
{
    private readonly LocalPosMemoryDbContext _dbContext;

    public IOrderRepository Orders => this;
    public IBillRepository Bills => this;
    public IStockRepository Stock => this;
    public IAuditRepository Audits => this;
    public IPosOutboxRepository Outbox => this;

    public LocalPosUnitOfWork(LocalPosMemoryDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // --- Order Repository ---
    Task IOrderRepository.AddAsync(Order order, CancellationToken cancellationToken)
    {
        _dbContext.Orders[order.Id] = order;
        return Task.CompletedTask;
    }

    Task<Order?> IOrderRepository.GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        _dbContext.Orders.TryGetValue(id, out var order);
        if (order != null && order.TenantId == tenantId)
            return Task.FromResult<Order?>(order);
        return Task.FromResult<Order?>(null);
    }

    Task IOrderRepository.UpdateAsync(Order order, CancellationToken cancellationToken)
    {
        _dbContext.Orders[order.Id] = order;
        return Task.CompletedTask;
    }

    // --- Bill Repository ---
    Task IBillRepository.AddAsync(Bill bill, CancellationToken cancellationToken)
    {
        _dbContext.Bills[bill.Id] = bill;
        return Task.CompletedTask;
    }

    Task<Bill?> IBillRepository.GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        _dbContext.Bills.TryGetValue(id, out var bill);
        if (bill != null && bill.TenantId == tenantId)
            return Task.FromResult<Bill?>(bill);
        return Task.FromResult<Bill?>(null);
    }

    Task<long> IBillRepository.GetNextDailySequenceNumberAsync(Guid tenantId, Guid branchId, DateOnly businessDate, CancellationToken cancellationToken)
    {
        long seq = _dbContext.GetNextDailySequence(tenantId, branchId, businessDate);
        return Task.FromResult(seq);
    }

    Task IBillRepository.UpdateAsync(Bill bill, CancellationToken cancellationToken)
    {
        _dbContext.Bills[bill.Id] = bill;
        return Task.CompletedTask;
    }

    // --- Stock Repository ---
    Task IStockRepository.AddMovementAsync(StockMovement movement, CancellationToken cancellationToken)
    {
        _dbContext.StockMovements[movement.Id] = movement;
        return Task.CompletedTask;
    }

    Task<IReadOnlyList<StockMovement>> IStockRepository.GetMovementsByReferenceAsync(Guid referenceId, Guid tenantId, CancellationToken cancellationToken)
    {
        var list = _dbContext.StockMovements.Values
            .Where(s => s.ReferenceTransactionId == referenceId && s.TenantId == tenantId)
            .ToList();
        return Task.FromResult<IReadOnlyList<StockMovement>>(list);
    }

    // --- Audit Repository ---
    Task IAuditRepository.AddEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        _dbContext.AuditEvents[auditEvent.Id] = auditEvent;
        return Task.CompletedTask;
    }

    Task<IReadOnlyList<AuditEvent>> IAuditRepository.GetEventsByReferenceAsync(Guid referenceId, Guid tenantId, CancellationToken cancellationToken)
    {
        var list = _dbContext.AuditEvents.Values
            .Where(a => a.ReferenceId == referenceId && a.TenantId == tenantId)
            .ToList();
        return Task.FromResult<IReadOnlyList<AuditEvent>>(list);
    }

    // --- Outbox Repository ---
    Task IPosOutboxRepository.AddMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _dbContext.OutboxMessages[message.EventId] = message;
        return Task.CompletedTask;
    }

    Task<IReadOnlyList<OutboxMessage>> IPosOutboxRepository.GetPendingMessagesAsync(Guid tenantId, int batchSize, CancellationToken cancellationToken)
    {
        var list = _dbContext.OutboxMessages.Values
            .Where(m => m.TenantId == tenantId && m.Status == OutboxStatus.Pending)
            .Take(batchSize)
            .ToList();
        return Task.FromResult<IReadOnlyList<OutboxMessage>>(list);
    }

    Task IPosOutboxRepository.MarkAsUploadedAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken)
    {
        foreach (var eventId in eventIds)
        {
            if (_dbContext.OutboxMessages.TryGetValue(eventId, out var msg))
            {
                msg.MarkSynced();
            }
        }
        return Task.CompletedTask;
    }

    // --- Transaction Commit ---
    public Task<int> CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        // All local writes to memory DbContext are atomic
        return Task.FromResult(1);
    }
}
