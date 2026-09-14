namespace Yashdeep.SyncEngine.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Yashdeep.Application.Outbox;
using Yashdeep.Application.Persistence;
using Yashdeep.Domain.Outbox;
using Yashdeep.Shared.Sync;
using Yashdeep.SyncEngine.Abstractions;

public class EdgeSyncProcessor : IEdgeSyncProcessor
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly ISyncRestApiClient _apiClient;
    private readonly ILocalUnitOfWork? _unitOfWork;
    private readonly SyncEngineOptions _options;
    private readonly ILogger<EdgeSyncProcessor> _logger;
    private readonly Random _random = new Random();

    public EdgeSyncProcessor(
        IOutboxRepository outboxRepository,
        ISyncRestApiClient apiClient,
        ILocalUnitOfWork? unitOfWork = null,
        SyncEngineOptions? options = null,
        ILogger<EdgeSyncProcessor>? logger = null)
    {
        _outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _unitOfWork = unitOfWork;
        _options = options ?? new SyncEngineOptions();
        _logger = logger ?? NullLogger<EdgeSyncProcessor>.Instance;
    }

    public async Task<SyncPushResponse> SynchronizeBatchAsync(
        Guid tenantId,
        Guid branchId,
        Guid deviceId,
        string authToken,
        CancellationToken cancellationToken = default)
    {
        DateTime nowUtc = DateTime.UtcNow;

        // 1. Fetch pending/failed events up to MaxBatchSize
        var candidates = await _outboxRepository.GetPendingEventsForDeviceAsync(deviceId, _options.MaxBatchSize, cancellationToken);
        if (candidates == null || !candidates.Any())
        {
            return new SyncPushResponse
            {
                Success = true,
                ServerTimeUtc = nowUtc,
                ItemResults = new List<SyncItemResult>()
            };
        }

        // Filter candidates that are ready for retry (NextRetryUtc <= nowUtc or null)
        var readyEvents = candidates
            .Where(e => e.Status == OutboxStatus.Pending ||
                       (e.Status == OutboxStatus.Failed && (e.NextRetryUtc == null || e.NextRetryUtc <= nowUtc)))
            .OrderBy(e => e.SequenceNumber)
            .Take(_options.MaxBatchSize)
            .ToList();

        if (!readyEvents.Any())
        {
            return new SyncPushResponse
            {
                Success = true,
                ServerTimeUtc = nowUtc,
                ItemResults = new List<SyncItemResult>()
            };
        }

        // 2. Mark in-flight and verify payload integrity
        var validBatchEvents = new List<OutboxMessage>();
        foreach (var message in readyEvents)
        {
            if (!message.VerifyPayloadIntegrity())
            {
                _logger.LogWarning("Outbox event {EventId} payload integrity check failed. Marking DeadLetter.", message.EventId);
                message.MarkDeadLetter(nowUtc, "Payload SHA-256 integrity check failed (tampered or corrupt payload).");
                await _outboxRepository.UpdateAsync(message, cancellationToken);
                continue;
            }

            message.MarkInFlight(nowUtc);
            await _outboxRepository.UpdateAsync(message, cancellationToken);
            validBatchEvents.Add(message);
        }

        if (_unitOfWork != null)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (!validBatchEvents.Any())
        {
            return new SyncPushResponse
            {
                Success = true,
                BatchError = "All candidates failed payload integrity check and were dead-lettered.",
                ServerTimeUtc = nowUtc
            };
        }

        // 3. Construct REST request
        var pushRequest = new SyncPushRequest
        {
            TenantId = tenantId,
            BranchId = branchId,
            DeviceId = deviceId,
            AuthToken = authToken,
            SentUtc = nowUtc,
            Events = validBatchEvents.Select(e => new IncomingEventEnvelope
            {
                EventId = e.EventId,
                AggregateType = e.AggregateType,
                AggregateId = e.AggregateId,
                TenantId = e.TenantId,
                BranchId = e.BranchId,
                DeviceId = e.DeviceId,
                SequenceNumber = e.SequenceNumber,
                EventType = e.EventType,
                EventVersion = e.EventVersion,
                CreatedUtc = e.CreatedUtc,
                PayloadJson = e.PayloadJson,
                PayloadHash = e.PayloadHash
            }).ToList()
        };

        // 4. Send HTTPS request
        SyncPushResponse response;
        try
        {
            response = await _apiClient.PushBatchAsync(pushRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP dispatch failed for device {DeviceId} batch.", deviceId);
            response = new SyncPushResponse
            {
                Success = false,
                BatchError = ex.Message,
                ServerTimeUtc = DateTime.UtcNow
            };
        }

        // 5. Handle response & update local outbox states
        DateTime processedUtc = response.ServerTimeUtc != default ? response.ServerTimeUtc : DateTime.UtcNow;

        if (!response.Success && (response.ItemResults == null || !response.ItemResults.Any()))
        {
            // Batch-level network or server 500 error: record failure on all items in batch
            string errorMessage = response.BatchError ?? "Unknown batch dispatch error";
            foreach (var message in validBatchEvents)
            {
                TimeSpan backoff = ComputeExponentialBackoff(message.RetryCount + 1);
                message.RecordFailure(errorMessage, null, processedUtc, backoff, _options.MaxRetries);
                await _outboxRepository.UpdateAsync(message, cancellationToken);
            }

            if (_unitOfWork != null)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return response;
        }

        // Process itemized results
        var resultMap = response.ItemResults.ToDictionary(r => r.EventId, r => r);

        foreach (var message in validBatchEvents)
        {
            if (resultMap.TryGetValue(message.EventId, out var itemResult))
            {
                switch (itemResult.Status)
                {
                    case SyncItemStatus.Ack:
                    case SyncItemStatus.Duplicate:
                        message.MarkSynced();
                        break;

                    case SyncItemStatus.Nack:
                        TimeSpan backoff = ComputeExponentialBackoff(message.RetryCount + 1);
                        string err = !string.IsNullOrEmpty(itemResult.ErrorMessage)
                            ? itemResult.ErrorMessage
                            : $"Item NACK ({itemResult.ErrorCode ?? "NACK"})";
                        message.RecordFailure(err, null, processedUtc, backoff, _options.MaxRetries);
                        break;

                    case SyncItemStatus.Ignored:
                        message.MarkDeadLetter(processedUtc, itemResult.ErrorMessage ?? "Ignored by cloud server.");
                        break;

                    default:
                        message.RecordFailure("Unknown item result status.", null, processedUtc, ComputeExponentialBackoff(message.RetryCount + 1), _options.MaxRetries);
                        break;
                }
            }
            else if (response.Success)
            {
                // Unmentioned item in a successful batch -> mark synced
                message.MarkSynced();
            }
            else
            {
                TimeSpan backoff = ComputeExponentialBackoff(message.RetryCount + 1);
                message.RecordFailure(response.BatchError ?? "Batch failed.", null, processedUtc, backoff, _options.MaxRetries);
            }

            await _outboxRepository.UpdateAsync(message, cancellationToken);
        }

        if (_unitOfWork != null)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return response;
    }

    public async Task<SyncQueueDiagnostics> GetDiagnosticsAsync(
        Guid tenantId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var allEvents = await _outboxRepository.GetEventsForDeviceAsync(deviceId, cancellationToken);

        int pendingCount = allEvents.Count(e => e.Status == OutboxStatus.Pending);
        int inFlightCount = allEvents.Count(e => e.Status == OutboxStatus.InFlight);
        int failedCount = allEvents.Count(e => e.Status == OutboxStatus.Failed);
        int deadLetterCount = allEvents.Count(e => e.Status == OutboxStatus.DeadLetter);
        int syncedCount = allEvents.Count(e => e.Status == OutboxStatus.Synced);

        var unsyncedEvents = allEvents
            .Where(e => e.Status != OutboxStatus.Synced)
            .OrderBy(e => e.CreatedUtc)
            .ToList();

        DateTime? oldestPending = unsyncedEvents.FirstOrDefault()?.CreatedUtc;

        bool isStuck = false;
        if (oldestPending.HasValue && (DateTime.UtcNow - oldestPending.Value) > _options.StuckThreshold)
        {
            isStuck = true;
        }

        string health = isStuck && oldestPending.HasValue
            ? $"Queue is STUCK. Oldest unsynced event created at {oldestPending.Value:u}."
            : deadLetterCount > 0
                ? $"Queue operational with {deadLetterCount} dead-lettered events."
                : "Queue healthy.";

        return new SyncQueueDiagnostics
        {
            PendingCount = pendingCount,
            InFlightCount = inFlightCount,
            FailedCount = failedCount,
            DeadLetterCount = deadLetterCount,
            SyncedCount = syncedCount,
            OldestPendingCreatedUtc = oldestPending,
            IsStuck = isStuck,
            HealthSummary = health
        };
    }

    private TimeSpan ComputeExponentialBackoff(int attempt)
    {
        double power = Math.Pow(2, attempt - 1);
        double baseSeconds = _options.BaseBackoff.TotalSeconds * power;
        // Apply 20% jitter
        double jitter = (0.8 + (_random.NextDouble() * 0.4));
        double finalSeconds = Math.Min(baseSeconds * jitter, _options.MaxBackoff.TotalSeconds);
        return TimeSpan.FromSeconds(finalSeconds);
    }
}
