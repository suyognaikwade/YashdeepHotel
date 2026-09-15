using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Sync.DTOs;
using Yashdeep.Domain.Sync;
using Yashdeep.Persistence.Cloud;
using Yashdeep.Shared.Sync;
using Yashdeep.SyncEngine.Abstractions;

namespace Yashdeep.SyncEngine.Services
{
    public sealed class CloudInboxProcessor : ICloudInboxProcessor
    {
        private readonly CloudDbContext _dbContext;

        public CloudInboxProcessor(CloudDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<SyncProcessingResult<TResponse>> ProcessEventAsync<TEvent, TResponse>(
            IncomingEventEnvelope envelope,
            Guid authenticatedTenantId,
            Func<TEvent, CancellationToken, Task<TResponse>> domainHandler,
            CancellationToken cancellationToken = default)
            where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(envelope);
            ArgumentNullException.ThrowIfNull(domainHandler);

            // 1. Tenant Authorization Filter: A Tenant A event must never be processed for Tenant B.
            if (envelope.TenantId != authenticatedTenantId)
            {
                return SyncProcessingResult<TResponse>.Failure(
                    InboxStatus.Rejected,
                    $"Tenant mismatch: Envelope TenantId '{envelope.TenantId}' does not match authenticated context '{authenticatedTenantId}'.");
            }

            string computedHash = envelope.ComputePayloadHash();

            // Deserialize payload for domain execution early to validate JSON format
            TEvent? eventPayload;
            try
            {
                eventPayload = JsonSerializer.Deserialize<TEvent>(envelope.PayloadJson);
                if (eventPayload == null)
                {
                    return SyncProcessingResult<TResponse>.Failure(
                        InboxStatus.Rejected,
                        "Event payload JSON deserialized to null.");
                }
            }
            catch (Exception ex)
            {
                return SyncProcessingResult<TResponse>.Failure(
                    InboxStatus.Rejected,
                    $"Failed to deserialize payload JSON: {ex.Message}");
            }

            // 2. Check for existing Inbox record using TenantId and EventId (ignoring global query filters if any)
            var existingMessage = await _dbContext.InboxMessages
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.TenantId == authenticatedTenantId && x.EventId == envelope.EventId, cancellationToken);

            if (existingMessage != null)
            {
                return await HandleExistingMessageAsync<TResponse>(existingMessage, computedHash, cancellationToken);
            }

            // 3. Process new event atomically within a transaction / execution strategy
            var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                var transaction = await TryBeginTransactionAsync(_dbContext, cancellationToken);

                try
                {
                    var inboxRecord = new InboxMessage
                    {
                        EventId = envelope.EventId,
                        TenantId = authenticatedTenantId,
                        BranchId = envelope.BranchId,
                        DeviceId = envelope.DeviceId,
                        EventType = envelope.EventType,
                        AggregateType = envelope.AggregateType,
                        AggregateId = envelope.AggregateId,
                        SequenceNumber = envelope.SequenceNumber,
                        PayloadHash = computedHash,
                        ReceivedAtUtc = DateTime.UtcNow,
                        Status = InboxStatus.Processing
                    };

                    await _dbContext.InboxMessages.AddAsync(inboxRecord, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    // Execute domain handler
                    TResponse responseData = await domainHandler(eventPayload, cancellationToken);

                    string serializedResponse = JsonSerializer.Serialize(responseData);
                    inboxRecord.Status = InboxStatus.Processed;
                    inboxRecord.ProcessedAtUtc = DateTime.UtcNow;
                    inboxRecord.ResponsePayloadJson = serializedResponse;

                    await _dbContext.SaveChangesAsync(cancellationToken);

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }

                    return SyncProcessingResult<TResponse>.Success(responseData, isDuplicate: false);
                }
                catch (DbUpdateException dbEx) when (IsDuplicateKeyException(dbEx))
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }

                    // Concurrent duplicate submission race condition recovery: re-read processed result (retry up to 10 times for in-flight transaction completion)
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        var reRead = await _dbContext.InboxMessages
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(x => x.TenantId == authenticatedTenantId && x.EventId == envelope.EventId, cancellationToken);

                        if (reRead != null && (reRead.Status == InboxStatus.Processed || reRead.Status == InboxStatus.PayloadMismatch || reRead.Status == InboxStatus.Rejected))
                        {
                            return await HandleExistingMessageAsync<TResponse>(reRead, computedHash, cancellationToken);
                        }

                        await Task.Delay(50, cancellationToken);
                    }

                    var finalReRead = await _dbContext.InboxMessages
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(x => x.TenantId == authenticatedTenantId && x.EventId == envelope.EventId, cancellationToken);

                    if (finalReRead != null)
                    {
                        return await HandleExistingMessageAsync<TResponse>(finalReRead, computedHash, cancellationToken);
                    }

                    return SyncProcessingResult<TResponse>.Failure(
                        InboxStatus.Failed,
                        $"Concurrent execution constraint conflict: {dbEx.Message}");
                }
                catch (Exception handlerEx)
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }

                    return SyncProcessingResult<TResponse>.Failure(
                        InboxStatus.Failed,
                        $"Domain operation execution failed: {handlerEx.Message}");
                }
                finally
                {
                    if (transaction != null)
                    {
                        await transaction.DisposeAsync();
                    }
                }
            });
        }

        private async Task<SyncProcessingResult<TResponse>> HandleExistingMessageAsync<TResponse>(
            InboxMessage existingMessage,
            string computedHash,
            CancellationToken cancellationToken)
        {
            // Integrity check: Event ID reuse with modified payload protection
            if (existingMessage.PayloadHash != computedHash)
            {
                existingMessage.Status = InboxStatus.PayloadMismatch;
                existingMessage.ErrorMessage = "Event ID reuse detected with modified payload hash.";
                existingMessage.DiagnosticsJson = JsonSerializer.Serialize(new
                {
                    ExistingHash = existingMessage.PayloadHash,
                    AttemptedHash = computedHash,
                    AttemptedAtUtc = DateTime.UtcNow
                });

                await _dbContext.SaveChangesAsync(cancellationToken);

                return SyncProcessingResult<TResponse>.Failure(
                    InboxStatus.PayloadMismatch,
                    "Event ID reuse detected with modified payload content.");
            }

            // Event replay handling: Return stored response without executing domain operation
            if (existingMessage.Status == InboxStatus.Processed && !string.IsNullOrEmpty(existingMessage.ResponsePayloadJson))
            {
                var cachedResponse = JsonSerializer.Deserialize<TResponse>(existingMessage.ResponsePayloadJson);
                return SyncProcessingResult<TResponse>.Duplicate(cachedResponse!);
            }

            if (existingMessage.Status == InboxStatus.PayloadMismatch || existingMessage.Status == InboxStatus.Rejected)
            {
                return SyncProcessingResult<TResponse>.Failure(
                    existingMessage.Status,
                    existingMessage.ErrorMessage ?? "Event was previously rejected.");
            }

            return SyncProcessingResult<TResponse>.Failure(
                existingMessage.Status,
                "Event is currently processing or in an incomplete state.");
        }

        private static async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> TryBeginTransactionAsync(CloudDbContext db, CancellationToken ct)
        {
            try
            {
                return await db.Database.BeginTransactionAsync(ct);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsDuplicateKeyException(DbUpdateException ex)
        {
            string message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("23505", StringComparison.OrdinalIgnoreCase);
        }
    }
}
