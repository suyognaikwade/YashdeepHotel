using System;
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
    }
}
