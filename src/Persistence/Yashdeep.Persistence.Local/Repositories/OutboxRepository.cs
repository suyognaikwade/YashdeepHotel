using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Outbox;
using Yashdeep.Domain.Outbox;

namespace Yashdeep.Persistence.Local.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly LocalPosDbContext _dbContext;

    public OutboxRepository(LocalPosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        _dbContext.OutboxMessages.Update(message);
        return Task.CompletedTask;
    }

    public async Task<OutboxMessage?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OutboxMessages.FirstOrDefaultAsync(m => m.EventId == eventId, cancellationToken);
    }

    public async Task<long> GetNextSequenceNumberAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        long maxSeq = await _dbContext.OutboxMessages
            .Where(m => m.DeviceId == deviceId)
            .MaxAsync(m => (long?)m.SequenceNumber, cancellationToken) ?? 0;

        return maxSeq + 1;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(Guid deviceId, int batchSize, CancellationToken cancellationToken = default)
    {
        return await GetPendingEventsForDeviceAsync(deviceId, batchSize, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingEventsForDeviceAsync(Guid deviceId, int batchSize, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OutboxMessages
            .Where(m => m.DeviceId == deviceId && (m.Status == OutboxStatus.Pending || m.Status == OutboxStatus.Failed))
            .OrderBy(m => m.SequenceNumber)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetEventsForDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OutboxMessages
            .Where(m => m.DeviceId == deviceId)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetByTenantAndDeviceAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.OutboxMessages
            .Where(m => m.TenantId == tenantId && m.DeviceId == deviceId)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync(cancellationToken);
    }
}
