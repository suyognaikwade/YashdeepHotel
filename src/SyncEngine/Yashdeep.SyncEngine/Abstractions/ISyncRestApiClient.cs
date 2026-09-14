namespace Yashdeep.SyncEngine.Abstractions;

using Yashdeep.Shared.Sync;

public interface ISyncRestApiClient
{
    Task<SyncPushResponse> PushBatchAsync(SyncPushRequest request, CancellationToken cancellationToken = default);
}
