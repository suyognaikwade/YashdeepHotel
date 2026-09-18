using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Domain.Outbox;
using Yashdeep.Persistence.Local;
using Yashdeep.Persistence.Local.Repositories;
using Xunit;

namespace Yashdeep.Outbox.Tests;

public class OutboxMetadataAndIntegrityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LocalPosDbContext> _options;

    public OutboxMetadataAndIntegrityTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LocalPosDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var dbContext = new LocalPosDbContext(_options);
        dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task EventIdentityUniqueness_PreventsDuplicateEventIds()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        using var dbContext1 = new LocalPosDbContext(_options);
        var repository1 = new OutboxRepository(dbContext1);

        var msg1 = new OutboxMessage(
            eventId, "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 1,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"key\":\"val1\"}");

        await repository1.AddAsync(msg1);
        await dbContext1.SaveChangesAsync();

        // Act & Assert - Attempting to insert another entity with identical EventId in a new context violates PK constraint
        using var dbContext2 = new LocalPosDbContext(_options);
        var repository2 = new OutboxRepository(dbContext2);

        var msg2 = new OutboxMessage(
            eventId, "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 2,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"key\":\"val2\"}");

        await repository2.AddAsync(msg2);

        await Assert.ThrowsAsync<DbUpdateException>(async () => await dbContext2.SaveChangesAsync());
    }

    [Fact]
    public async Task ContextVerification_StoresAndFiltersTenantBranchAndDeviceContext()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        var branch1 = Guid.NewGuid();
        var branch2 = Guid.NewGuid();
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();

        using var dbContext = new LocalPosDbContext(_options);
        var repository = new OutboxRepository(dbContext);

        var msg1 = new OutboxMessage(
            Guid.NewGuid(), "Order", Guid.NewGuid(), tenant1, branch1, device1, 1,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"tenant\":1}");

        var msg2 = new OutboxMessage(
            Guid.NewGuid(), "Order", Guid.NewGuid(), tenant2, branch2, device2, 1,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"tenant\":2}");

        await repository.AddAsync(msg1);
        await repository.AddAsync(msg2);
        await dbContext.SaveChangesAsync();

        // Act
        var tenant1Messages = await repository.GetByTenantAndDeviceAsync(tenant1, device1);
        var tenant2Messages = await repository.GetByTenantAndDeviceAsync(tenant2, device2);

        // Assert
        Assert.Single(tenant1Messages);
        Assert.Equal(tenant1, tenant1Messages[0].TenantId);
        Assert.Equal(branch1, tenant1Messages[0].BranchId);
        Assert.Equal(device1, tenant1Messages[0].DeviceId);

        Assert.Single(tenant2Messages);
        Assert.Equal(tenant2, tenant2Messages[0].TenantId);
        Assert.Equal(branch2, tenant2Messages[0].BranchId);
        Assert.Equal(device2, tenant2Messages[0].DeviceId);
    }

    [Fact]
    public async Task DeterministicSequenceNumbering_GeneratesSequentialSequencePerDevice()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        using var dbContext = new LocalPosDbContext(_options);
        var repository = new OutboxRepository(dbContext);

        // Act & Assert
        long seq1 = await repository.GetNextSequenceNumberAsync(deviceId);
        Assert.Equal(1, seq1);

        var msg1 = new OutboxMessage(
            Guid.NewGuid(), "Order", Guid.NewGuid(), tenantId, branchId, deviceId, seq1,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"seq\":1}");
        await repository.AddAsync(msg1);
        await dbContext.SaveChangesAsync();

        long seq2 = await repository.GetNextSequenceNumberAsync(deviceId);
        Assert.Equal(2, seq2);

        var msg2 = new OutboxMessage(
            Guid.NewGuid(), "Order", Guid.NewGuid(), tenantId, branchId, deviceId, seq2,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"seq\":2}");
        await repository.AddAsync(msg2);
        await dbContext.SaveChangesAsync();

        long seq3 = await repository.GetNextSequenceNumberAsync(deviceId);
        Assert.Equal(3, seq3);
    }

    [Fact]
    public async Task DuplicateSequencePerDevice_EnforcesDatabaseUniquenessConstraint()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        using var dbContext1 = new LocalPosDbContext(_options);
        var repository1 = new OutboxRepository(dbContext1);

        var msg1 = new OutboxMessage(
            Guid.NewGuid(), "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 1,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"seq\":1}");

        await repository1.AddAsync(msg1);
        await dbContext1.SaveChangesAsync();

        using var dbContext2 = new LocalPosDbContext(_options);
        var repository2 = new OutboxRepository(dbContext2);

        var msg2 = new OutboxMessage(
            Guid.NewGuid(), "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 1,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"seq\":1_dup}");

        await repository2.AddAsync(msg2);

        // Assert - Same DeviceId and SequenceNumber violates unique index across transactions
        await Assert.ThrowsAsync<DbUpdateException>(async () => await dbContext2.SaveChangesAsync());
    }

    [Fact]
    public void PayloadIntegrity_FailsWhenPayloadSilentlyTampered()
    {
        // Arrange
        string originalPayload = "{\"billNumber\":\"INV-1001\",\"total\":1250.00}";
        var msg = new OutboxMessage(
            Guid.NewGuid(), "Bill", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            "BillGeneratedEvent", 1, DateTime.UtcNow, originalPayload);

        // Assert - Initial payload verification passes
        Assert.True(msg.VerifyPayloadIntegrity());

        // Act - Simulate silent payload tampering via reflection
        var payloadProp = typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.PayloadJson))!;
        payloadProp.SetValue(msg, "{\"billNumber\":\"INV-1001\",\"total\":250.00}"); // Tampered amount!

        // Assert - Payload checksum verification detects tampering
        Assert.False(msg.VerifyPayloadIntegrity());
    }

    [Fact]
    public void OutboxStateTransitions_HandlesRetryAndDeadLetterTransitionsSafely()
    {
        // Arrange
        var msg = new OutboxMessage(
            Guid.NewGuid(), "Order", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"test\":true}");

        Assert.Equal(OutboxStatus.Pending, msg.Status);
        Assert.Equal(0, msg.RetryCount);

        // Act 1: Mark InFlight
        var attemptedUtc = DateTime.UtcNow;
        msg.MarkInFlight(attemptedUtc);
        Assert.Equal(OutboxStatus.InFlight, msg.Status);
        Assert.Equal(attemptedUtc, msg.LastAttemptedUtc);

        // Act 2: Record transient failure (Retries 1 to 4)
        for (int i = 1; i <= 4; i++)
        {
            var failUtc = DateTime.UtcNow;
            msg.RecordFailure($"Network Error {i}", "Stack trace detail", failUtc, TimeSpan.FromMinutes(i), maxRetries: 5);
            Assert.Equal(OutboxStatus.Failed, msg.Status);
            Assert.Equal(i, msg.RetryCount);
            Assert.Equal($"Network Error {i}", msg.LastError);
            Assert.NotNull(msg.NextRetryUtc);
        }

        // Act 3: 5th Failure triggers DeadLetter
        var finalFailUtc = DateTime.UtcNow;
        msg.RecordFailure("Fatal Server 500", "Stack trace detail 5", finalFailUtc, maxRetries: 5);
        Assert.Equal(OutboxStatus.DeadLetter, msg.Status);
        Assert.Equal(5, msg.RetryCount);
        Assert.NotNull(msg.DeadLetteredUtc);

        // Assert invalid transition check
        Assert.Throws<InvalidOperationException>(() => msg.MarkSynced());
    }

    [Fact]
    public async Task ProcessRestartWithUnsentMessages_PersistsToDiskAndRecoversPendingOutboxMessages()
    {
        // Arrange
        string tempDbFile = Path.Combine(Path.GetTempPath(), $"outbox_test_{Guid.NewGuid():N}.db");
        var fileOptions = new DbContextOptionsBuilder<LocalPosDbContext>()
            .UseSqlite($"Data Source={tempDbFile}")
            .Options;

        try
        {
            var tenantId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();

            // 1. Initial process lifecycle: Create DB and add 2 outbox messages
            using (var dbContext = new LocalPosDbContext(fileOptions))
            {
                await dbContext.Database.EnsureCreatedAsync();
                var repository = new OutboxRepository(dbContext);

                var msg1 = new OutboxMessage(
                    Guid.NewGuid(), "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 1,
                    "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"order\":1}");

                var msg2 = new OutboxMessage(
                    Guid.NewGuid(), "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 2,
                    "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"order\":2}");

                await repository.AddAsync(msg1);
                await repository.AddAsync(msg2);
                await dbContext.SaveChangesAsync();
            } // Process crash / shutdown (DbContext disposed and connection closed)

            // 2. Simulated process restart: Open fresh DbContext connected to existing SQLite DB file
            using (var restartContext = new LocalPosDbContext(fileOptions))
            {
                var repository = new OutboxRepository(restartContext);

                // Act - Recover pending outbox messages after restart
                var pendingBatch = await repository.GetPendingBatchAsync(deviceId, batchSize: 50);

                // Assert - Messages survived process restart and remain pending/recoverable
                Assert.Equal(2, pendingBatch.Count);
                Assert.Equal(1, pendingBatch[0].SequenceNumber);
                Assert.Equal(2, pendingBatch[1].SequenceNumber);
                Assert.All(pendingBatch, m => Assert.Equal(OutboxStatus.Pending, m.Status));
                Assert.All(pendingBatch, m => Assert.Equal(tenantId, m.TenantId));
                Assert.All(pendingBatch, m => Assert.Equal(branchId, m.BranchId));
                Assert.All(pendingBatch, m => Assert.Equal(deviceId, m.DeviceId));
            }
        }
        finally
        {
            if (File.Exists(tempDbFile))
            {
                File.Delete(tempDbFile);
            }
        }
    }

    [Fact]
    public async Task PreAckSafety_EventsRemainUnsyncedUntilExplicitCloudAck_AndDeadLetterCannotMarkSynced()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using var dbContext = new LocalPosDbContext(_options);
        var repository = new OutboxRepository(dbContext);

        var msg = new OutboxMessage(
            eventId, "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 1,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"order\":10}");

        await repository.AddAsync(msg);
        await dbContext.SaveChangesAsync();

        // Act & Assert 1: Event starts in Pending state (never marked Synced prior to cloud ACK)
        var fetched1 = await repository.GetByEventIdAsync(eventId);
        Assert.NotNull(fetched1);
        Assert.Equal(OutboxStatus.Pending, fetched1.Status);

        // Act & Assert 2: Transition to InFlight when batch upload starts
        fetched1.MarkInFlight(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();

        var fetched2 = await repository.GetByEventIdAsync(eventId);
        Assert.NotNull(fetched2);
        Assert.Equal(OutboxStatus.InFlight, fetched2.Status);

        // Act & Assert 3: Mark Synced ONLY upon cloud ACK
        fetched2.MarkSynced();
        await dbContext.SaveChangesAsync();

        var fetched3 = await repository.GetByEventIdAsync(eventId);
        Assert.NotNull(fetched3);
        Assert.Equal(OutboxStatus.Synced, fetched3.Status);

        // Act & Assert 4: Verify invalid state transition (DeadLetter cannot be marked Synced)
        var deadLetterMsg = new OutboxMessage(
            Guid.NewGuid(), "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 2,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"dead\":true}");
        deadLetterMsg.MarkDeadLetter(DateTime.UtcNow, "Max retries exceeded");

        Assert.Throws<InvalidOperationException>(() => deadLetterMsg.MarkSynced());
    }

    [Fact]
    public void EventVersionValidation_ConstructsWithValidVersion_AndRejectsInvalidVersion()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        // Act 1: Construct with valid version 1 & version 2
        var msgV1 = new OutboxMessage(
            Guid.NewGuid(), "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 1,
            "OrderPlacedEvent", 1, DateTime.UtcNow, "{\"v\":1}");

        var msgV2 = new OutboxMessage(
            Guid.NewGuid(), "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 2,
            "OrderPlacedEvent", 2, DateTime.UtcNow, "{\"v\":2}");

        // Assert 1
        Assert.Equal(1, msgV1.EventVersion);
        Assert.Equal(2, msgV2.EventVersion);

        // Act & Assert 2: Constructing with EventVersion <= 0 throws ArgumentOutOfRangeException
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OutboxMessage(
                Guid.NewGuid(), "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 3,
                "OrderPlacedEvent", 0, DateTime.UtcNow, "{\"v\":0}"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OutboxMessage(
                Guid.NewGuid(), "Order", Guid.NewGuid(), tenantId, branchId, deviceId, 4,
                "OrderPlacedEvent", -1, DateTime.UtcNow, "{\"v\":-1}"));
    }
}
