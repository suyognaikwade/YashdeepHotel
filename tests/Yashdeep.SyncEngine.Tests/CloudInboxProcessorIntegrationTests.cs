using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Application.Sync.DTOs;
using Yashdeep.Domain.Sync;
using Yashdeep.Persistence.Cloud;
using Yashdeep.Shared.Sync;
using Yashdeep.SyncEngine.Services;

namespace Yashdeep.SyncEngine.Tests
{
    public class SampleOrderPlacedEvent
    {
        public Guid OrderId { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }

    public class SampleOrderResult
    {
        public Guid OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
    }

    public class CloudInboxProcessorIntegrationTests
    {
        private DbContextOptions<CloudDbContext> CreateNewInMemoryOptions()
        {
            // Use unique in-memory database name per test context to simulate isolated persistent storage
            return new DbContextOptionsBuilder<CloudDbContext>()
                .UseSqlite($"DataSource=file:{Guid.NewGuid()}?mode=memory&cache=shared")
                .Options;
        }

        private CloudDbContext CreateDbContext(DbContextOptions<CloudDbContext> options, Guid tenantId = default)
        {
            ITenantContext? tenantContext = tenantId != Guid.Empty ? new TenantContext(tenantId) : null;
            return new CloudDbContext(options, tenantContext);
        }

        [Fact]
        public async Task ProcessEventAsync_FirstSubmission_ExecutesDomainHandlerAndStoresProcessedInbox()
        {
            var options = CreateNewInMemoryOptions();
            Guid tenantId = Guid.NewGuid();
            Guid eventId = Guid.NewGuid();
            int executionCount = 0;

            using (var initDb = CreateDbContext(options, tenantId))
            {
                initDb.Database.EnsureCreated();
            }

            var envelope = new IncomingEventEnvelope
            {
                EventId = eventId,
                TenantId = tenantId,
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = Guid.NewGuid(),
                SequenceNumber = 1,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent
                {
                    OrderId = Guid.NewGuid(),
                    TableNumber = "T-101",
                    TotalAmount = 500.00m
                })
            };

            using var db = CreateDbContext(options, tenantId);
            var processor = new CloudInboxProcessor(db);

            var result = await processor.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                envelope,
                tenantId,
                (evt, ct) =>
                {
                    executionCount++;
                    return Task.FromResult(new SampleOrderResult
                    {
                        OrderId = evt.OrderId,
                        Status = "Committed",
                        ProcessedAt = DateTime.UtcNow
                    });
                });

            Assert.True(result.IsSuccess);
            Assert.False(result.IsDuplicate);
            Assert.Equal(InboxStatus.Processed, result.Status);
            Assert.NotNull(result.Data);
            Assert.Equal("Committed", result.Data!.Status);
            Assert.Equal(1, executionCount);

            var inboxRecord = await db.InboxMessages.FirstOrDefaultAsync(x => x.EventId == eventId && x.TenantId == tenantId);
            Assert.NotNull(inboxRecord);
            Assert.Equal(InboxStatus.Processed, inboxRecord!.Status);
            Assert.Equal(envelope.ComputePayloadHash(), inboxRecord.PayloadHash);
        }

        [Fact]
        public async Task ProcessEventAsync_DuplicateSubmission_DoesNotReExecuteDomainHandler_ReturnsCachedResult()
        {
            var options = CreateNewInMemoryOptions();
            Guid tenantId = Guid.NewGuid();
            Guid eventId = Guid.NewGuid();
            Guid orderId = Guid.NewGuid();
            int executionCount = 0;

            using (var initDb = CreateDbContext(options, tenantId))
            {
                initDb.Database.EnsureCreated();
            }

            var envelope = new IncomingEventEnvelope
            {
                EventId = eventId,
                TenantId = tenantId,
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = orderId,
                SequenceNumber = 1,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent
                {
                    OrderId = orderId,
                    TableNumber = "T-102",
                    TotalAmount = 750.00m
                })
            };

            Task<SampleOrderResult> DomainHandler(SampleOrderPlacedEvent evt, CancellationToken ct)
            {
                executionCount++;
                return Task.FromResult(new SampleOrderResult
                {
                    OrderId = evt.OrderId,
                    Status = "Committed",
                    ProcessedAt = DateTime.UtcNow
                });
            }

            // Submission 1
            using (var db1 = CreateDbContext(options, tenantId))
            {
                var processor1 = new CloudInboxProcessor(db1);
                var result1 = await processor1.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    envelope, tenantId, DomainHandler);

                Assert.True(result1.IsSuccess);
                Assert.False(result1.IsDuplicate);
            }

            // Submission 2 (Duplicate Retry after network timeout)
            using (var db2 = CreateDbContext(options, tenantId))
            {
                var processor2 = new CloudInboxProcessor(db2);
                var result2 = await processor2.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    envelope, tenantId, DomainHandler);

                Assert.True(result2.IsSuccess);
                Assert.True(result2.IsDuplicate);
                Assert.Equal(InboxStatus.Processed, result2.Status);
                Assert.NotNull(result2.Data);
                Assert.Equal(orderId, result2.Data!.OrderId);
            }

            // Domain operation MUST execute exactly once
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task ProcessEventAsync_TenantMismatch_RejectsEventAndDoesNotExecuteDomainHandler()
        {
            var options = CreateNewInMemoryOptions();
            Guid tenantA = Guid.NewGuid();
            Guid tenantB = Guid.NewGuid();
            int executionCount = 0;

            using (var initDb = CreateDbContext(options, tenantB))
            {
                initDb.Database.EnsureCreated();
            }

            var envelope = new IncomingEventEnvelope
            {
                EventId = Guid.NewGuid(),
                TenantId = tenantA, // Envelope specifies Tenant A
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = Guid.NewGuid(),
                SequenceNumber = 1,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent { OrderId = Guid.NewGuid() })
            };

            using var db = CreateDbContext(options, tenantB);
            var processor = new CloudInboxProcessor(db);

            // Attempt to process Tenant A envelope under Tenant B authenticated context
            var result = await processor.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                envelope,
                tenantB, // Authenticated Tenant B context
                (evt, ct) =>
                {
                    executionCount++;
                    return Task.FromResult(new SampleOrderResult());
                });

            Assert.False(result.IsSuccess);
            Assert.Equal(InboxStatus.Rejected, result.Status);
            Assert.Contains("Tenant mismatch", result.ErrorMessage);
            Assert.Equal(0, executionCount);
        }

        [Fact]
        public async Task ProcessEventAsync_SameEventUnderDifferentTenant_ProcessedIndependently()
        {
            var options = CreateNewInMemoryOptions();
            Guid tenantA = Guid.NewGuid();
            Guid tenantB = Guid.NewGuid();
            Guid sharedEventId = Guid.NewGuid();
            int executionCountA = 0;
            int executionCountB = 0;

            using (var initDb = CreateDbContext(options))
            {
                initDb.Database.EnsureCreated();
            }

            var envelopeA = new IncomingEventEnvelope
            {
                EventId = sharedEventId,
                TenantId = tenantA,
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = Guid.NewGuid(),
                SequenceNumber = 1,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent { OrderId = Guid.NewGuid(), TotalAmount = 100m })
            };

            var envelopeB = new IncomingEventEnvelope
            {
                EventId = sharedEventId, // Same EventId
                TenantId = tenantB,      // Different Tenant
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = Guid.NewGuid(),
                SequenceNumber = 1,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent { OrderId = Guid.NewGuid(), TotalAmount = 200m })
            };

            // Process under Tenant A
            using (var dbA = CreateDbContext(options, tenantA))
            {
                var processorA = new CloudInboxProcessor(dbA);
                var resA = await processorA.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    envelopeA, tenantA, (evt, ct) => { executionCountA++; return Task.FromResult(new SampleOrderResult { Status = "TenantA_OK" }); });
                Assert.True(resA.IsSuccess);
            }

            // Process under Tenant B
            using (var dbB = CreateDbContext(options, tenantB))
            {
                var processorB = new CloudInboxProcessor(dbB);
                var resB = await processorB.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    envelopeB, tenantB, (evt, ct) => { executionCountB++; return Task.FromResult(new SampleOrderResult { Status = "TenantB_OK" }); });
                Assert.True(resB.IsSuccess);
            }

            Assert.Equal(1, executionCountA);
            Assert.Equal(1, executionCountB);

            // Verify both Inbox records exist independently under their respective tenant composite key
            using (var dbCheck = CreateDbContext(options))
            {
                var msgA = await dbCheck.InboxMessages.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantA && x.EventId == sharedEventId);
                var msgB = await dbCheck.InboxMessages.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantB && x.EventId == sharedEventId);
                Assert.NotNull(msgA);
                Assert.NotNull(msgB);
                Assert.NotEqual(msgA!.TenantId, msgB!.TenantId);
            }
        }

        [Fact]
        public async Task ProcessEventAsync_EventIdReuseWithModifiedPayload_RejectsEventAndStoresPayloadMismatch()
        {
            var options = CreateNewInMemoryOptions();
            Guid tenantId = Guid.NewGuid();
            Guid reusedEventId = Guid.NewGuid();

            using (var initDb = CreateDbContext(options, tenantId))
            {
                initDb.Database.EnsureCreated();
            }

            var originalEnvelope = new IncomingEventEnvelope
            {
                EventId = reusedEventId,
                TenantId = tenantId,
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = Guid.NewGuid(),
                SequenceNumber = 1,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent
                {
                    OrderId = Guid.NewGuid(),
                    TableNumber = "T-10",
                    TotalAmount = 100.00m
                })
            };

            var tamperedEnvelope = new IncomingEventEnvelope
            {
                EventId = reusedEventId, // Same EventId
                TenantId = tenantId,
                BranchId = originalEnvelope.BranchId,
                DeviceId = originalEnvelope.DeviceId,
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = originalEnvelope.AggregateId,
                SequenceNumber = 1,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent
                {
                    OrderId = Guid.NewGuid(),
                    TableNumber = "T-10",
                    TotalAmount = 99999.00m // Tampered amount
                })
            };

            // 1. Process original
            using (var db1 = CreateDbContext(options, tenantId))
            {
                var processor1 = new CloudInboxProcessor(db1);
                await processor1.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    originalEnvelope, tenantId, (evt, ct) => Task.FromResult(new SampleOrderResult()));
            }

            // 2. Process tampered
            using (var db2 = CreateDbContext(options, tenantId))
            {
                var processor2 = new CloudInboxProcessor(db2);
                var tamperedResult = await processor2.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    tamperedEnvelope, tenantId, (evt, ct) => Task.FromResult(new SampleOrderResult()));

                Assert.False(tamperedResult.IsSuccess);
                Assert.Equal(InboxStatus.PayloadMismatch, tamperedResult.Status);
                Assert.Contains("modified payload", tamperedResult.ErrorMessage);
            }

            // Verify Inbox record updated to PayloadMismatch with diagnostics
            using (var dbRead = CreateDbContext(options, tenantId))
            {
                var inbox = await dbRead.InboxMessages.FirstOrDefaultAsync(x => x.EventId == reusedEventId);
                Assert.NotNull(inbox);
                Assert.Equal(InboxStatus.PayloadMismatch, inbox!.Status);
                Assert.NotNull(inbox.DiagnosticsJson);
            }
        }

        [Fact]
        public async Task ProcessEventAsync_ConcurrentDuplicateSubmission_RecoversSafelyWithoutDuplicateDomainExecution()
        {
            var options = CreateNewInMemoryOptions();
            Guid tenantId = Guid.NewGuid();
            Guid eventId = Guid.NewGuid();
            int executionCount = 0;

            using (var initDb = CreateDbContext(options, tenantId))
            {
                initDb.Database.EnsureCreated();
            }

            var envelope = new IncomingEventEnvelope
            {
                EventId = eventId,
                TenantId = tenantId,
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = Guid.NewGuid(),
                SequenceNumber = 1,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent
                {
                    OrderId = Guid.NewGuid(),
                    TableNumber = "T-Concurrent",
                    TotalAmount = 300.00m
                })
            };

            Task<SampleOrderResult> ConcurrentDomainHandler(SampleOrderPlacedEvent evt, CancellationToken ct)
            {
                Interlocked.Increment(ref executionCount);
                return Task.FromResult(new SampleOrderResult
                {
                    OrderId = evt.OrderId,
                    Status = "CommittedConcurrent"
                });
            }

            // First submission completes
            using (var dbInitial = CreateDbContext(options, tenantId))
            {
                var processorInitial = new CloudInboxProcessor(dbInitial);
                var initialResult = await processorInitial.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    envelope, tenantId, ConcurrentDomainHandler);
                Assert.True(initialResult.IsSuccess);
            }

            // Run 5 sequential retries against separate context instances mimicking server cluster nodes receiving retries
            for (int i = 0; i < 5; i++)
            {
                using var db = CreateDbContext(options, tenantId);
                var processor = new CloudInboxProcessor(db);
                var result = await processor.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    envelope, tenantId, ConcurrentDomainHandler);

                Assert.True(result.IsSuccess);
                Assert.True(result.IsDuplicate);
            }

            // Domain handler executed exactly once across initial submission + all retries
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task ProcessEventAsync_TrueParallelConcurrentSubmissions_ExecutesDomainHandlerOnceOnly()
        {
            var options = CreateNewInMemoryOptions();
            Guid tenantId = Guid.NewGuid();
            Guid eventId = Guid.NewGuid();
            Guid orderId = Guid.NewGuid();
            int executionCount = 0;

            using (var initDb = CreateDbContext(options, tenantId))
            {
                initDb.Database.EnsureCreated();
            }

            var envelope = new IncomingEventEnvelope
            {
                EventId = eventId,
                TenantId = tenantId,
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = orderId,
                SequenceNumber = 1,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent
                {
                    OrderId = orderId,
                    TableNumber = "T-Parallel",
                    TotalAmount = 1250.00m
                })
            };

            int taskCount = 8;
            var tasks = new List<Task<SyncProcessingResult<SampleOrderResult>>>();

            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var db = CreateDbContext(options, tenantId);
                    var processor = new CloudInboxProcessor(db);
                    return await processor.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                        envelope,
                        tenantId,
                        async (evt, ct) =>
                        {
                            Interlocked.Increment(ref executionCount);
                            await Task.Delay(20, ct); // Simulate non-trivial domain processing delay
                            return new SampleOrderResult
                            {
                                OrderId = evt.OrderId,
                                Status = "ParallelCommitted",
                                ProcessedAt = DateTime.UtcNow
                            };
                        });
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Exactly 1 execution of domain handler
            Assert.Equal(1, executionCount);

            // All tasks must report success
            Assert.All(results, r => Assert.True(r.IsSuccess));

            // Exactly 1 non-duplicate, and (taskCount - 1) duplicates returning cached data
            Assert.Equal(1, results.Count(r => !r.IsDuplicate));
            Assert.Equal(taskCount - 1, results.Count(r => r.IsDuplicate));
            Assert.All(results, r => Assert.Equal(orderId, r.Data!.OrderId));
        }

        [Fact]
        public async Task ProcessEventAsync_DomainHandlerFailure_RollsBackTransaction()
        {
            var options = CreateNewInMemoryOptions();
            Guid tenantId = Guid.NewGuid();
            Guid eventId = Guid.NewGuid();

            using (var initDb = CreateDbContext(options, tenantId))
            {
                initDb.Database.EnsureCreated();
            }

            var envelope = new IncomingEventEnvelope
            {
                EventId = eventId,
                TenantId = tenantId,
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = Guid.NewGuid(),
                SequenceNumber = 1,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent { OrderId = Guid.NewGuid() })
            };

            using var db = CreateDbContext(options, tenantId);
            var processor = new CloudInboxProcessor(db);

            var result = await processor.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                envelope,
                tenantId,
                (evt, ct) => throw new InvalidOperationException("Simulated domain business rule failure"));

            Assert.False(result.IsSuccess);
            Assert.Equal(InboxStatus.Failed, result.Status);
            Assert.Contains("Simulated domain business rule failure", result.ErrorMessage);

            // Transaction rolled back; InboxMessage should NOT exist in DB
            var inboxRecord = await db.InboxMessages.FirstOrDefaultAsync(x => x.EventId == eventId);
            Assert.Null(inboxRecord);
        }

        [Fact]
        public async Task ProcessEventAsync_MalformedJsonPayload_ReturnsRejectedWithoutExecutingDomainHandler()
        {
            var options = CreateNewInMemoryOptions();
            Guid tenantId = Guid.NewGuid();
            int executionCount = 0;

            using (var initDb = CreateDbContext(options, tenantId))
            {
                initDb.Database.EnsureCreated();
            }

            var malformedEnvelope = new IncomingEventEnvelope
            {
                EventId = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = Guid.NewGuid(),
                SequenceNumber = 1,
                PayloadJson = "{ invalid json payload syntax ... }"
            };

            using var db = CreateDbContext(options, tenantId);
            var processor = new CloudInboxProcessor(db);

            var result = await processor.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                malformedEnvelope,
                tenantId,
                (evt, ct) => { executionCount++; return Task.FromResult(new SampleOrderResult()); });

            Assert.False(result.IsSuccess);
            Assert.Equal(InboxStatus.Rejected, result.Status);
            Assert.Contains("Failed to deserialize payload JSON", result.ErrorMessage);
            Assert.Equal(0, executionCount);
        }

        [Fact]
        public async Task ProcessEventAsync_ServerCommitFollowedByLostResponse_ReturnsCachedResultOnRetry()
        {
            var options = CreateNewInMemoryOptions();
            Guid tenantId = Guid.NewGuid();
            Guid eventId = Guid.NewGuid();
            Guid orderId = Guid.NewGuid();
            int executionCount = 0;

            using (var initDb = CreateDbContext(options, tenantId))
            {
                initDb.Database.EnsureCreated();
            }

            var envelope = new IncomingEventEnvelope
            {
                EventId = eventId,
                TenantId = tenantId,
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = orderId,
                SequenceNumber = 101,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent { OrderId = orderId, TableNumber = "T-LostResponse" })
            };

            // Attempt 1: Server processes successfully and commits, but response dropped on wire
            using (var db1 = CreateDbContext(options, tenantId))
            {
                var processor1 = new CloudInboxProcessor(db1);
                var initialResult = await processor1.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    envelope,
                    tenantId,
                    (evt, ct) =>
                    {
                        executionCount++;
                        return Task.FromResult(new SampleOrderResult { OrderId = orderId, Status = "CommittedOnServer" });
                    });

                Assert.True(initialResult.IsSuccess);
                // Assume HTTP connection dropped here before client receives initialResult
            }

            // Attempt 2: Client retries the request
            using (var db2 = CreateDbContext(options, tenantId))
            {
                var processor2 = new CloudInboxProcessor(db2);
                var retryResult = await processor2.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    envelope,
                    tenantId,
                    (evt, ct) =>
                    {
                        executionCount++;
                        return Task.FromResult(new SampleOrderResult { OrderId = orderId, Status = "ReExecutedShouldNotHappen" });
                    });

                Assert.True(retryResult.IsSuccess);
                Assert.True(retryResult.IsDuplicate);
                Assert.NotNull(retryResult.Data);
                Assert.Equal("CommittedOnServer", retryResult.Data!.Status);
            }

            // Business logic ran exactly once
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task ProcessEventAsync_ApplicationRestartSimulation_RetrievesCachedResponseFromFreshDbContext()
        {
            var options = CreateNewInMemoryOptions();
            Guid tenantId = Guid.NewGuid();
            Guid eventId = Guid.NewGuid();
            Guid orderId = Guid.NewGuid();

            using (var initDb = CreateDbContext(options, tenantId))
            {
                initDb.Database.EnsureCreated();
            }

            var envelope = new IncomingEventEnvelope
            {
                EventId = eventId,
                TenantId = tenantId,
                BranchId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                EventType = "OrderPlacedEvent",
                AggregateType = "Order",
                AggregateId = orderId,
                SequenceNumber = 1,
                PayloadJson = JsonSerializer.Serialize(new SampleOrderPlacedEvent { OrderId = orderId, TableNumber = "T-Restart" })
            };

            // Pre-restart execution
            using (var dbPreRestart = CreateDbContext(options, tenantId))
            {
                var processor = new CloudInboxProcessor(dbPreRestart);
                await processor.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    envelope,
                    tenantId,
                    (evt, ct) => Task.FromResult(new SampleOrderResult { OrderId = orderId, Status = "PreRestartSuccess" }));
            }

            // Post-restart simulation (brand new DbContext instance)
            using (var dbPostRestart = CreateDbContext(options, tenantId))
            {
                var processor = new CloudInboxProcessor(dbPostRestart);
                var result = await processor.ProcessEventAsync<SampleOrderPlacedEvent, SampleOrderResult>(
                    envelope,
                    tenantId,
                    (evt, ct) => throw new InvalidOperationException("Handler should not run post-restart duplicate"));

                Assert.True(result.IsSuccess);
                Assert.True(result.IsDuplicate);
                Assert.Equal("PreRestartSuccess", result.Data!.Status);
            }
        }
    }
}
