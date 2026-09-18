namespace Yashdeep.Shared.Sync;

public record SyncPushRequest
{
    public Guid TenantId { get; init; }
    public Guid BranchId { get; init; }
    public Guid DeviceId { get; init; }
    public string AuthToken { get; init; } = string.Empty;
    public DateTime SentUtc { get; init; } = DateTime.UtcNow;
    public List<IncomingEventEnvelope> Events { get; init; } = new();
}

public enum SyncItemStatus
{
    Ack = 1,
    Nack = 2,
    Duplicate = 3,
    Ignored = 4
}

public record SyncItemResult
{
    public Guid EventId { get; init; }
    public long SequenceNumber { get; init; }
    public SyncItemStatus Status { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

public record SyncPushResponse
{
    public bool Success { get; init; }
    public string? BatchError { get; init; }
    public DateTime ServerTimeUtc { get; init; } = DateTime.UtcNow;
    public List<SyncItemResult> ItemResults { get; init; } = new();
    public List<Guid> ProcessedEventIds => ItemResults
        .Where(r => r.Status == SyncItemStatus.Ack || r.Status == SyncItemStatus.Duplicate)
        .Select(r => r.EventId)
        .ToList();
}

public record SyncQueueDiagnostics
{
    public int PendingCount { get; init; }
    public int InFlightCount { get; init; }
    public int FailedCount { get; init; }
    public int DeadLetterCount { get; init; }
    public int SyncedCount { get; init; }
    public DateTime? OldestPendingCreatedUtc { get; init; }
    public bool IsStuck { get; init; }
    public string? HealthSummary { get; init; }
}
