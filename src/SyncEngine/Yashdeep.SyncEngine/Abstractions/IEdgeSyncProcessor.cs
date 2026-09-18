namespace Yashdeep.SyncEngine.Abstractions;

using Yashdeep.Shared.Sync;

public record SyncEngineOptions
{
    public int MaxBatchSize { get; init; } = 50;
    public int MaxRetries { get; init; } = 5;
    public TimeSpan BaseBackoff { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan StuckThreshold { get; init; } = TimeSpan.FromHours(1);
}

public interface IEdgeSyncProcessor
{
    Task<SyncPushResponse> SynchronizeBatchAsync(
        Guid tenantId,
        Guid branchId,
        Guid deviceId,
        string authToken,
        CancellationToken cancellationToken = default);

    Task<SyncQueueDiagnostics> GetDiagnosticsAsync(
        Guid tenantId,
        Guid deviceId,
        CancellationToken cancellationToken = default);
}
