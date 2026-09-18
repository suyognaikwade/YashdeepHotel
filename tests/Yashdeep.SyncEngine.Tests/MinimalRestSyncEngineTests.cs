namespace Yashdeep.SyncEngine.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Yashdeep.Domain.Outbox;
using Yashdeep.Persistence.Local;
using Yashdeep.Persistence.Local.Repositories;
using Yashdeep.Shared.Events;
using Yashdeep.Shared.Sync;
using Yashdeep.SyncEngine.Abstractions;
using Yashdeep.SyncEngine.Services;
using Xunit;

public class MinimalRestSyncEngineTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly LocalPosDbContext _localDbContext;
    private readonly OutboxRepository _outboxRepository;
    private readonly LocalUnitOfWork _unitOfWork;
    private readonly MockSyncRestApiClient _mockApiClient;
    private readonly EdgeSyncProcessor _syncProcessor;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _deviceId = Guid.NewGuid();
    private readonly string _authToken = "test-jwt-token";

    public MinimalRestSyncEngineTests()
    {
        _sqliteConnection = new SqliteConnection("DataSource=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<LocalPosDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _localDbContext = new LocalPosDbContext(options);
        _localDbContext.Database.EnsureCreated();

        _outboxRepository = new OutboxRepository(_localDbContext);
        _unitOfWork = new LocalUnitOfWork(_localDbContext);
        _mockApiClient = new MockSyncRestApiClient();
        _syncProcessor = new EdgeSyncProcessor(
            _outboxRepository,
            _mockApiClient,
            _unitOfWork,
            new SyncEngineOptions
            {
                MaxBatchSize = 5,
                MaxRetries = 3,
                BaseBackoff = TimeSpan.FromMilliseconds(100),
                MaxBackoff = TimeSpan.FromMilliseconds(500),
                StuckThreshold = TimeSpan.FromHours(1)
            });
    }

    public void Dispose()
    {
        _localDbContext.Dispose();
        _sqliteConnection.Dispose();
    }

    private OutboxMessage CreateTestOutboxMessage(long seqNum, string payloadText = "{\"TestKey\":\"TestVal\"}")
    {
        var evt = new TestOutboxEvent
        {
            EventId = Guid.NewGuid(),
            AggregateId = Guid.NewGuid(),
            TenantId = _tenantId,
            BranchId = _branchId,
            DeviceId = _deviceId,
            CreatedUtc = DateTime.UtcNow
        };
        return OutboxMessage.FromEvent(evt, seqNum, payloadText);
    }

    [Fact]
    public async Task SynchronizeBatchAsync_SuccessfulPush_MarksOutboxMessagesAsSynced()
    {
        // Arrange
        var msg1 = CreateTestOutboxMessage(1);
        var msg2 = CreateTestOutboxMessage(2);
        await _outboxRepository.AddAsync(msg1);
        await _outboxRepository.AddAsync(msg2);
        await _unitOfWork.SaveChangesAsync();

        _mockApiClient.ResponseHandler = req => new SyncPushResponse
        {
            Success = true,
            ServerTimeUtc = DateTime.UtcNow,
            ItemResults = req.Events.Select(e => new SyncItemResult
            {
                EventId = e.EventId,
                SequenceNumber = e.SequenceNumber,
                Status = SyncItemStatus.Ack
            }).ToList()
        };

        // Act
        var result = await _syncProcessor.SynchronizeBatchAsync(_tenantId, _branchId, _deviceId, _authToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, _mockApiClient.LastRequest?.Events.Count);

        _localDbContext.ChangeTracker.Clear();
        var dbMsg1 = await _outboxRepository.GetByEventIdAsync(msg1.EventId);
        var dbMsg2 = await _outboxRepository.GetByEventIdAsync(msg2.EventId);

        Assert.NotNull(dbMsg1);
        Assert.NotNull(dbMsg2);
        Assert.Equal(OutboxStatus.Synced, dbMsg1.Status);
        Assert.Equal(OutboxStatus.Synced, dbMsg2.Status);
    }

    [Fact]
    public async Task SynchronizeBatchAsync_DuplicatePush_MarksOutboxMessageAsSynced()
    {
        // Arrange
        var msg = CreateTestOutboxMessage(1);
        await _outboxRepository.AddAsync(msg);
        await _unitOfWork.SaveChangesAsync();

        _mockApiClient.ResponseHandler = req => new SyncPushResponse
        {
            Success = true,
            ServerTimeUtc = DateTime.UtcNow,
            ItemResults = new List<SyncItemResult>
            {
                new SyncItemResult
                {
                    EventId = msg.EventId,
                    SequenceNumber = msg.SequenceNumber,
                    Status = SyncItemStatus.Duplicate,
                    ErrorMessage = "Already processed in cloud inbox."
                }
            }
        };

        // Act
        var result = await _syncProcessor.SynchronizeBatchAsync(_tenantId, _branchId, _deviceId, _authToken);

        // Assert
        Assert.True(result.Success);
        _localDbContext.ChangeTracker.Clear();
        var dbMsg = await _outboxRepository.GetByEventIdAsync(msg.EventId);
        Assert.NotNull(dbMsg);
        Assert.Equal(OutboxStatus.Synced, dbMsg.Status);
    }

    [Fact]
    public async Task SynchronizeBatchAsync_Server500Failure_IncrementsRetryAndAppliesBackoff()
    {
        // Arrange
        var msg = CreateTestOutboxMessage(1);
        await _outboxRepository.AddAsync(msg);
        await _unitOfWork.SaveChangesAsync();

        _mockApiClient.ResponseHandler = req => new SyncPushResponse
        {
            Success = false,
            BatchError = "HTTP 500 Internal Server Error",
            ServerTimeUtc = DateTime.UtcNow
        };

        // Act
        var result = await _syncProcessor.SynchronizeBatchAsync(_tenantId, _branchId, _deviceId, _authToken);

        // Assert
        Assert.False(result.Success);
        _localDbContext.ChangeTracker.Clear();
        var dbMsg = await _outboxRepository.GetByEventIdAsync(msg.EventId);
        Assert.NotNull(dbMsg);
        Assert.Equal(OutboxStatus.Failed, dbMsg.Status);
        Assert.Equal(1, dbMsg.RetryCount);
        Assert.NotNull(dbMsg.NextRetryUtc);
        Assert.Contains("HTTP 500", dbMsg.LastError);
    }

    [Fact]
    public async Task SynchronizeBatchAsync_ExceedsMaxRetries_QuarantinesToDeadLetter()
    {
        // Arrange
        var msg = CreateTestOutboxMessage(1);
        await _outboxRepository.AddAsync(msg);
        await _unitOfWork.SaveChangesAsync();

        _mockApiClient.ResponseHandler = req => new SyncPushResponse
        {
            Success = false,
            BatchError = "Persistent Server Exception",
            ServerTimeUtc = DateTime.UtcNow
        };

        // Act - Retry up to max limit (3 retries in options)
        for (int i = 0; i < 3; i++)
        {
            _localDbContext.ChangeTracker.Clear();
            var target = await _outboxRepository.GetByEventIdAsync(msg.EventId);
            if (target?.Status == OutboxStatus.Failed)
            {
                _localDbContext.Entry(target).Property("NextRetryUtc").CurrentValue = DateTime.UtcNow.AddMinutes(-1);
                await _unitOfWork.SaveChangesAsync();
            }

            await _syncProcessor.SynchronizeBatchAsync(_tenantId, _branchId, _deviceId, _authToken);
        }

        // Assert
        _localDbContext.ChangeTracker.Clear();
        var deadMsg = await _outboxRepository.GetByEventIdAsync(msg.EventId);
        Assert.NotNull(deadMsg);
        Assert.Equal(OutboxStatus.DeadLetter, deadMsg.Status);
        Assert.Equal(3, deadMsg.RetryCount);
        Assert.NotNull(deadMsg.DeadLetteredUtc);
    }

    [Fact]
    public async Task SynchronizeBatchAsync_PartialBatchFailure_HandlesAckAndNackIndividually()
    {
        // Arrange
        var msg1 = CreateTestOutboxMessage(1);
        var msg2 = CreateTestOutboxMessage(2);
        await _outboxRepository.AddAsync(msg1);
        await _outboxRepository.AddAsync(msg2);
        await _unitOfWork.SaveChangesAsync();

        _mockApiClient.ResponseHandler = req => new SyncPushResponse
        {
            Success = true,
            ServerTimeUtc = DateTime.UtcNow,
            ItemResults = new List<SyncItemResult>
            {
                new SyncItemResult { EventId = msg1.EventId, SequenceNumber = 1, Status = SyncItemStatus.Ack },
                new SyncItemResult { EventId = msg2.EventId, SequenceNumber = 2, Status = SyncItemStatus.Nack, ErrorCode = "ERR_VAL", ErrorMessage = "Invalid schema payload" }
            }
        };

        // Act
        var result = await _syncProcessor.SynchronizeBatchAsync(_tenantId, _branchId, _deviceId, _authToken);

        // Assert
        Assert.True(result.Success);
        _localDbContext.ChangeTracker.Clear();
        var dbMsg1 = await _outboxRepository.GetByEventIdAsync(msg1.EventId);
        var dbMsg2 = await _outboxRepository.GetByEventIdAsync(msg2.EventId);

        Assert.Equal(OutboxStatus.Synced, dbMsg1?.Status);
        Assert.Equal(OutboxStatus.Failed, dbMsg2?.Status);
        Assert.Equal(1, dbMsg2?.RetryCount);
        Assert.Contains("Invalid schema payload", dbMsg2?.LastError);
    }

    [Fact]
    public async Task SynchronizeBatchAsync_TamperedPayload_MovesToDeadLetterWithoutHttpCall()
    {
        // Arrange
        var msg = CreateTestOutboxMessage(1);
        await _outboxRepository.AddAsync(msg);
        await _unitOfWork.SaveChangesAsync();

        // Tamper with PayloadJson directly in DB without updating PayloadHash
        _localDbContext.Entry(msg).Property(m => m.PayloadJson).CurrentValue = "{\"Tampered\":true}";
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _syncProcessor.SynchronizeBatchAsync(_tenantId, _branchId, _deviceId, _authToken);

        // Assert
        Assert.Null(_mockApiClient.LastRequest); // HTTP call should NOT be made for tampered message
        _localDbContext.ChangeTracker.Clear();
        var dbMsg = await _outboxRepository.GetByEventIdAsync(msg.EventId);
        Assert.NotNull(dbMsg);
        Assert.Equal(OutboxStatus.DeadLetter, dbMsg.Status);
        Assert.Contains("integrity check failed", dbMsg.LastError);
    }

    [Fact]
    public async Task SynchronizeBatchAsync_RestartWithPendingOutbox_ResumesAndFlushesPendingEvents()
    {
        // Arrange
        var msg = CreateTestOutboxMessage(1);
        await _outboxRepository.AddAsync(msg);
        await _unitOfWork.SaveChangesAsync();

        // Simulate app crash while in-flight
        msg.MarkInFlight(DateTime.UtcNow.AddMinutes(-5));
        await _outboxRepository.UpdateAsync(msg);
        await _unitOfWork.SaveChangesAsync();

        // Reset InFlight status to Pending (as local DB startup routine would do)
        _localDbContext.Entry(msg).Property(m => m.Status).CurrentValue = OutboxStatus.Pending;
        await _unitOfWork.SaveChangesAsync();
        _localDbContext.ChangeTracker.Clear();

        // Simulate app restart by creating fresh processor instance over same DB
        var freshProcessor = new EdgeSyncProcessor(
            _outboxRepository,
            _mockApiClient,
            _unitOfWork,
            new SyncEngineOptions());

        _mockApiClient.ResponseHandler = req => new SyncPushResponse
        {
            Success = true,
            ServerTimeUtc = DateTime.UtcNow,
            ItemResults = req.Events.Select(e => new SyncItemResult
            {
                EventId = e.EventId,
                SequenceNumber = e.SequenceNumber,
                Status = SyncItemStatus.Ack
            }).ToList()
        };

        // Act
        var result = await freshProcessor.SynchronizeBatchAsync(_tenantId, _branchId, _deviceId, _authToken);

        // Assert
        Assert.True(result.Success);
        _localDbContext.ChangeTracker.Clear();
        var dbMsg = await _outboxRepository.GetByEventIdAsync(msg.EventId);
        Assert.NotNull(dbMsg);
        Assert.Equal(OutboxStatus.Synced, dbMsg.Status);
    }

    [Fact]
    public async Task GetDiagnosticsAsync_StuckQueue_IdentifiesStuckStatus()
    {
        // Arrange
        var msgOld = CreateTestOutboxMessage(1);
        await _outboxRepository.AddAsync(msgOld);
        await _unitOfWork.SaveChangesAsync();

        // Set CreatedUtc to 2 hours in the past
        _localDbContext.Entry(msgOld).Property(m => m.CreatedUtc).CurrentValue = DateTime.UtcNow.AddHours(-2);
        await _unitOfWork.SaveChangesAsync();
        _localDbContext.ChangeTracker.Clear();

        // Act
        var diagnostics = await _syncProcessor.GetDiagnosticsAsync(_tenantId, _deviceId);

        // Assert
        Assert.Equal(1, diagnostics.PendingCount);
        Assert.True(diagnostics.IsStuck);
        Assert.Contains("STUCK", diagnostics.HealthSummary);
    }

    private record TestOutboxEvent : OutboxEventBase
    {
        public override string AggregateType => "FoundationalTest";
        public override string EventType => "SyncEngine.FoundationalTestEvent";
    }

    private class MockSyncRestApiClient : ISyncRestApiClient
    {
        public SyncPushRequest? LastRequest { get; private set; }
        public Func<SyncPushRequest, SyncPushResponse>? ResponseHandler { get; set; }

        public Task<SyncPushResponse> PushBatchAsync(SyncPushRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (ResponseHandler != null)
            {
                return Task.FromResult(ResponseHandler(request));
            }

            return Task.FromResult(new SyncPushResponse
            {
                Success = true,
                ServerTimeUtc = DateTime.UtcNow,
                ItemResults = request.Events.Select(e => new SyncItemResult
                {
                    EventId = e.EventId,
                    SequenceNumber = e.SequenceNumber,
                    Status = SyncItemStatus.Ack
                }).ToList()
            });
        }
    }
}
