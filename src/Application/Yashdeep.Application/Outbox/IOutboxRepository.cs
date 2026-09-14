using Yashdeep.Domain.Outbox;

namespace Yashdeep.Application.Outbox;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<OutboxMessage?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<long> GetNextSequenceNumberAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(Guid deviceId, int batchSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetPendingEventsForDeviceAsync(Guid deviceId, int batchSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetEventsForDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetByTenantAndDeviceAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default);
}
