using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Domain.Sync;
using Yashdeep.Persistence.Cloud;
using Xunit;

namespace Yashdeep.Persistence.Cloud.Tests
{
    public class CloudDbContextPersistenceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<CloudDbContext> _options;

        public CloudDbContextPersistenceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<CloudDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new CloudDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }


        [Fact]
        public async Task TenantIsolation_TenantACannotSeeTenantBData()
        {
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            // Seed database with messages for both tenants
            using (var seedContext = new CloudDbContext(_options))
            {
                seedContext.InboxMessages.AddRange(
                    new InboxMessage
                    {
                        EventId = Guid.NewGuid(),
                        TenantId = tenantA,
                        BranchId = Guid.NewGuid(),
                        DeviceId = Guid.NewGuid(),
                        EventType = "OrderPlaced",
                        AggregateType = "Order",
                        AggregateId = Guid.NewGuid(),
                        SequenceNumber = 1,
                        PayloadHash = "hash1",
                        Status = InboxStatus.Processed,
                        ReceivedAtUtc = DateTime.UtcNow
                    },
                    new InboxMessage
                    {
                        EventId = Guid.NewGuid(),
                        TenantId = tenantB,
                        BranchId = Guid.NewGuid(),
                        DeviceId = Guid.NewGuid(),
                        EventType = "OrderPlaced",
                        AggregateType = "Order",
                        AggregateId = Guid.NewGuid(),
                        SequenceNumber = 1,
                        PayloadHash = "hash2",
                        Status = InboxStatus.Processed,
                        ReceivedAtUtc = DateTime.UtcNow
                    }
                );
                await seedContext.SaveChangesAsync();
            }

            // Query using Tenant A context
            var tenantAContext = new TenantContext(tenantA);
            using (var dbA = new CloudDbContext(_options, tenantAContext))
            {
                var messages = await dbA.InboxMessages.ToListAsync();
                Assert.Single(messages);
                Assert.All(messages, m => Assert.Equal(tenantA, m.TenantId));
            }

            // Query using Tenant B context
            var tenantBContext = new TenantContext(tenantB);
            using (var dbB = new CloudDbContext(_options, tenantBContext))
            {
                var messages = await dbB.InboxMessages.ToListAsync();
                Assert.Single(messages);
                Assert.All(messages, m => Assert.Equal(tenantB, m.TenantId));
            }
        }

        [Fact]
        public async Task MissingTenantContext_ReturnsAllRecords()
        {
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            using (var seedContext = new CloudDbContext(_options))
            {
                seedContext.InboxMessages.AddRange(
                    new InboxMessage
                    {
                        EventId = Guid.NewGuid(),
                        TenantId = tenantA,
                        BranchId = Guid.NewGuid(),
                        DeviceId = Guid.NewGuid(),
                        EventType = "OrderPlaced",
                        AggregateType = "Order",
                        AggregateId = Guid.NewGuid(),
                        SequenceNumber = 1,
                        PayloadHash = "hash1",
                        Status = InboxStatus.Processed,
                        ReceivedAtUtc = DateTime.UtcNow
                    },
                    new InboxMessage
                    {
                        EventId = Guid.NewGuid(),
                        TenantId = tenantB,
                        BranchId = Guid.NewGuid(),
                        DeviceId = Guid.NewGuid(),
                        EventType = "OrderPlaced",
                        AggregateType = "Order",
                        AggregateId = Guid.NewGuid(),
                        SequenceNumber = 1,
                        PayloadHash = "hash2",
                        Status = InboxStatus.Processed,
                        ReceivedAtUtc = DateTime.UtcNow
                    }
                );
                await seedContext.SaveChangesAsync();
            }

            // Null tenant context
            using (var dbNoContext = new CloudDbContext(_options, null))
            {
                var messages = await dbNoContext.InboxMessages.ToListAsync();
                Assert.Equal(2, messages.Count);
            }

            // Empty tenant context ID
            var emptyContext = new TenantContext(Guid.Empty);
            using (var dbEmptyContext = new CloudDbContext(_options, emptyContext))
            {
                var messages = await dbEmptyContext.InboxMessages.ToListAsync();
                Assert.Equal(2, messages.Count);
            }
        }

        [Fact]
        public async Task DirectDatabaseAccess_IgnoreQueryFilters_BypassesApplicationFilters()
        {
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            using (var seedContext = new CloudDbContext(_options))
            {
                seedContext.InboxMessages.AddRange(
                    new InboxMessage
                    {
                        EventId = Guid.NewGuid(),
                        TenantId = tenantA,
                        BranchId = Guid.NewGuid(),
                        DeviceId = Guid.NewGuid(),
                        EventType = "OrderPlaced",
                        AggregateType = "Order",
                        AggregateId = Guid.NewGuid(),
                        SequenceNumber = 1,
                        PayloadHash = "hash1",
                        Status = InboxStatus.Processed,
                        ReceivedAtUtc = DateTime.UtcNow
                    },
                    new InboxMessage
                    {
                        EventId = Guid.NewGuid(),
                        TenantId = tenantB,
                        BranchId = Guid.NewGuid(),
                        DeviceId = Guid.NewGuid(),
                        EventType = "OrderPlaced",
                        AggregateType = "Order",
                        AggregateId = Guid.NewGuid(),
                        SequenceNumber = 1,
                        PayloadHash = "hash2",
                        Status = InboxStatus.Processed,
                        ReceivedAtUtc = DateTime.UtcNow
                    }
                );
                await seedContext.SaveChangesAsync();
            }

            var tenantAContext = new TenantContext(tenantA);
            using (var dbA = new CloudDbContext(_options, tenantAContext))
            {
                // Standard query adheres to query filter
                var filteredMessages = await dbA.InboxMessages.ToListAsync();
                Assert.Single(filteredMessages);

                // Direct database query bypassing filters retrieves all tenant records
                var unfilteredMessages = await dbA.InboxMessages.IgnoreQueryFilters().ToListAsync();
                Assert.Equal(2, unfilteredMessages.Count);
            }
        }

        [Fact]
        public async Task DuplicatePrimaryKey_ThrowsDbUpdateException()
        {
            var tenantId = Guid.NewGuid();
            var eventId = Guid.NewGuid();

            using (var db1 = new CloudDbContext(_options))
            {
                var msg1 = new InboxMessage
                {
                    EventId = eventId,
                    TenantId = tenantId,
                    BranchId = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    EventType = "OrderPlaced",
                    AggregateType = "Order",
                    AggregateId = Guid.NewGuid(),
                    SequenceNumber = 1,
                    PayloadHash = "hash1",
                    Status = InboxStatus.Processed,
                    ReceivedAtUtc = DateTime.UtcNow
                };

                await db1.InboxMessages.AddAsync(msg1);
                await db1.SaveChangesAsync();
            }

            using (var db2 = new CloudDbContext(_options))
            {
                var msg2 = new InboxMessage
                {
                    EventId = eventId,
                    TenantId = tenantId,
                    BranchId = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    EventType = "OrderPlaced",
                    AggregateType = "Order",
                    AggregateId = Guid.NewGuid(),
                    SequenceNumber = 2,
                    PayloadHash = "hash2",
                    Status = InboxStatus.Processed,
                    ReceivedAtUtc = DateTime.UtcNow
                };

                await db2.InboxMessages.AddAsync(msg2);
                await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
            }
        }

        [Fact]
        public async Task TransactionRollback_RevertsUncommittedChanges()
        {
            var tenantId = Guid.NewGuid();

            using (var db = new CloudDbContext(_options))
            {
                using var transaction = await db.Database.BeginTransactionAsync();

                db.InboxMessages.Add(new InboxMessage
                {
                    EventId = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    EventType = "OrderPlaced",
                    AggregateType = "Order",
                    AggregateId = Guid.NewGuid(),
                    SequenceNumber = 1,
                    PayloadHash = "hash1",
                    Status = InboxStatus.Processing,
                    ReceivedAtUtc = DateTime.UtcNow
                });

                await db.SaveChangesAsync();

                // Rollback explicitly
                await transaction.RollbackAsync();
            }

            using (var dbCheck = new CloudDbContext(_options))
            {
                var messages = await dbCheck.InboxMessages.ToListAsync();
                Assert.Empty(messages);
            }
        }
    }
}
