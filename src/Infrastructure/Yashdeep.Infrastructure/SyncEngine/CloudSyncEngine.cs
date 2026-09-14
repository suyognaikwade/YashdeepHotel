using System.Collections.Concurrent;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Domain.Entities.Sync;

namespace Yashdeep.Infrastructure.SyncEngine;

public class CloudInboxProcessor
{
    private readonly ConcurrentDictionary<string, OutboxMessage> _cloudProcessedEvents = new();

    /// <summary>
    /// Processes an incoming outbox message batch on the cloud server idempotently.
    /// Composite key: (TenantId, EventId).
    /// Returns true if accepted (or already accepted), and records whether it was a duplicate replay.
    /// </summary>
    public (bool Accepted, bool IsDuplicate) ProcessInboxMessage(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        string compositeKey = $"{message.TenantId}_{message.EventId}";

        if (_cloudProcessedEvents.ContainsKey(compositeKey))
        {
            // Duplicate replay detected - server responds successfully (ACK) without re-executing logic
            return (Accepted: true, IsDuplicate: true);
        }

        bool added = _cloudProcessedEvents.TryAdd(compositeKey, message);
        return (Accepted: added, IsDuplicate: false);
    }

    public int GetProcessedMessageCount(Guid tenantId)
    {
        return _cloudProcessedEvents.Values.Count(v => v.TenantId == tenantId);
    }

    public bool HasMessage(Guid tenantId, Guid eventId)
    {
        string compositeKey = $"{tenantId}_{eventId}";
        return _cloudProcessedEvents.ContainsKey(compositeKey);
    }

    public void ClearInbox()
    {
        _cloudProcessedEvents.Clear();
    }
}

public class CloudSyncEngine : ICloudSyncEngine
{
    private readonly IPosOutboxRepository _outboxRepository;
    private readonly CloudInboxProcessor _inboxProcessor;
    private bool _isOnline = true;

    public CloudSyncEngine(
        IPosOutboxRepository outboxRepository,
        CloudInboxProcessor inboxProcessor)
    {
        _outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
        _inboxProcessor = inboxProcessor ?? throw new ArgumentNullException(nameof(inboxProcessor));
    }

    public void SetNetworkAvailable(bool available)
    {
        _isOnline = available;
    }

    public Task<bool> IsNetworkAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_isOnline);
    }

    public async Task<int> SynchronizePendingOutboxAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (!_isOnline)
            return 0;

        var pendingMessages = await _outboxRepository.GetPendingMessagesAsync(tenantId, 100, cancellationToken);
        if (pendingMessages.Count == 0)
            return 0;

        var uploadedEventIds = new List<Guid>();

        foreach (var message in pendingMessages)
        {
            var (accepted, _) = _inboxProcessor.ProcessInboxMessage(message);
            if (accepted)
            {
                uploadedEventIds.Add(message.EventId);
            }
        }

        if (uploadedEventIds.Count > 0)
        {
            await _outboxRepository.MarkAsUploadedAsync(uploadedEventIds, cancellationToken);
        }

        return uploadedEventIds.Count;
    }
}
