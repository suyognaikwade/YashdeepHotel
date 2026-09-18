namespace Yashdeep.SyncEngine.Workers;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Yashdeep.Shared.Sync;
using Yashdeep.SyncEngine.Abstractions;

public class EdgeSyncBackgroundService : BackgroundService
{
    private readonly IEdgeSyncProcessor _syncProcessor;
    private readonly Guid _tenantId;
    private readonly Guid _branchId;
    private readonly Guid _deviceId;
    private readonly Func<Task<string>> _authTokenProvider;
    private readonly TimeSpan _syncInterval;
    private readonly ILogger<EdgeSyncBackgroundService> _logger;

    public EdgeSyncBackgroundService(
        IEdgeSyncProcessor syncProcessor,
        Guid tenantId,
        Guid branchId,
        Guid deviceId,
        Func<Task<string>> authTokenProvider,
        TimeSpan? syncInterval = null,
        ILogger<EdgeSyncBackgroundService>? logger = null)
    {
        _syncProcessor = syncProcessor ?? throw new ArgumentNullException(nameof(syncProcessor));
        _tenantId = tenantId;
        _branchId = branchId;
        _deviceId = deviceId;
        _authTokenProvider = authTokenProvider ?? throw new ArgumentNullException(nameof(authTokenProvider));
        _syncInterval = syncInterval ?? TimeSpan.FromSeconds(15);
        _logger = logger ?? NullLogger<EdgeSyncBackgroundService>.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EdgeSyncBackgroundService started for device {DeviceId}.", _deviceId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                string token = await _authTokenProvider();
                SyncPushResponse response = await _syncProcessor.SynchronizeBatchAsync(
                    _tenantId,
                    _branchId,
                    _deviceId,
                    token,
                    stoppingToken);

                if (!response.Success)
                {
                    _logger.LogWarning("Sync batch processing failed: {Error}", response.BatchError);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("EdgeSyncBackgroundService stopping gracefully due to cancellation.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception in EdgeSyncBackgroundService loop.");
            }

            try
            {
                await Task.Delay(_syncInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("EdgeSyncBackgroundService stopped.");
    }
}
