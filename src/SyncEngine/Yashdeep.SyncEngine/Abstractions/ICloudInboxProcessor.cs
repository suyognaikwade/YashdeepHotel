using System;
using System.Threading;
using System.Threading.Tasks;
using Yashdeep.Application.Sync.DTOs;
using Yashdeep.Shared.Sync;

namespace Yashdeep.SyncEngine.Abstractions
{
    public interface ICloudInboxProcessor
    {
        Task<SyncProcessingResult<TResponse>> ProcessEventAsync<TEvent, TResponse>(
            IncomingEventEnvelope envelope,
            Guid authenticatedTenantId,
            Func<TEvent, CancellationToken, Task<TResponse>> domainHandler,
            CancellationToken cancellationToken = default)
            where TEvent : class;
    }
}
