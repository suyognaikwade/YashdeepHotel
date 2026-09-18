using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Domain.Entities.Orders;
using Yashdeep.Domain.Outbox;
using Yashdeep.Domain.ValueObjects;
using Yashdeep.Persistence.Local;
using Yashdeep.Persistence.Local.Repositories;
using Yashdeep.Shared.Events;
using Xunit;

namespace Yashdeep.Outbox.Tests;

public record OrderPlacedEvent : OutboxEventBase
{
    public override string AggregateType => "Order";
    public override string EventType => "OrderPlacedEvent";
    public string OrderNumber { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
}

public class OutboxAtomicityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LocalPosDbContext> _options;

    public OutboxAtomicityTests()
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
    public async Task RollbackTransaction_DoesNotPersistOutboxEventOrBusinessMutation()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using var dbContext = new LocalPosDbContext(_options);
        var unitOfWork = new LocalUnitOfWork(dbContext);
        var repository = new OutboxRepository(dbContext);

        // Act
        await using (var transaction = await unitOfWork.BeginTransactionAsync())
        {
            var order = new Order(orderId, tenantId, branchId, locationId, "T-1", SectionTier.Ac, OrderType.DineIn, "ORD-001", DateOnly.FromDateTime(DateTime.UtcNow), Guid.NewGuid(), "Captain");
            dbContext.Orders.Add(order);

            var domainEvent = new OrderPlacedEvent
            {
                EventId = eventId,
                AggregateId = orderId,
                TenantId = tenantId,
                BranchId = branchId,
                DeviceId = deviceId,
                OrderNumber = "ORD-001",
                TotalAmount = 500.00m
            };

            var outboxMessage = OutboxMessage.FromEvent(domainEvent, 1, "{\"orderNumber\":\"ORD-001\",\"amount\":500.00}");
            await repository.AddAsync(outboxMessage);

            await unitOfWork.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        // Assert - Verify using a fresh DbContext instance
        using var verifyContext = new LocalPosDbContext(_options);
        var persistedOrder = await verifyContext.Orders.FindAsync(orderId);
        var persistedOutbox = await verifyContext.SyncOutboxMessages.FindAsync(eventId);

        Assert.Null(persistedOrder);
        Assert.Null(persistedOutbox);
    }

    [Fact]
    public async Task CommitTransaction_PersistsBothBusinessMutationAndOutboxEventAtomically()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using var dbContext = new LocalPosDbContext(_options);
        var unitOfWork = new LocalUnitOfWork(dbContext);
        var repository = new OutboxRepository(dbContext);

        // Act
        await using (var transaction = await unitOfWork.BeginTransactionAsync())
        {
            var order = new Order(orderId, tenantId, branchId, locationId, "T-1", SectionTier.Ac, OrderType.DineIn, "ORD-002", DateOnly.FromDateTime(DateTime.UtcNow), Guid.NewGuid(), "Captain");
            dbContext.Orders.Add(order);

            var domainEvent = new OrderPlacedEvent
            {
                EventId = eventId,
                AggregateId = orderId,
                TenantId = tenantId,
                BranchId = branchId,
                DeviceId = deviceId,
                OrderNumber = "ORD-002",
                TotalAmount = 750.00m
            };

            var outboxMessage = OutboxMessage.FromEvent(domainEvent, 1, "{\"orderNumber\":\"ORD-002\",\"amount\":750.00}");
            await repository.AddAsync(outboxMessage);

            await unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // Assert - Verify using a fresh DbContext instance
        using var verifyContext = new LocalPosDbContext(_options);
        var persistedOrder = await verifyContext.Orders.FindAsync(orderId);
        var persistedOutbox = await verifyContext.SyncOutboxMessages.FindAsync(eventId);

        Assert.NotNull(persistedOrder);
        Assert.Equal("ORD-002", persistedOrder.OrderNumber);
        Assert.NotNull(persistedOutbox);
        Assert.Equal(eventId, persistedOutbox.EventId);
        Assert.Equal(tenantId, persistedOutbox.TenantId);
        Assert.Equal(branchId, persistedOutbox.BranchId);
        Assert.Equal(deviceId, persistedOutbox.DeviceId);
        Assert.Equal(OutboxStatus.Pending, persistedOutbox.Status);
    }

    [Fact]
    public async Task ApplicationFailure_MidwayThroughTransaction_RollsBackAllMutationsAndOutboxMessages()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using (var dbContext = new LocalPosDbContext(_options))
        {
            var unitOfWork = new LocalUnitOfWork(dbContext);
            var repository = new OutboxRepository(dbContext);

            // Act - Simulate an unhandled exception thrown during transaction execution prior to commit
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await using (var transaction = await unitOfWork.BeginTransactionAsync())
                {
                    var order = new LocalOrder(orderId, tenantId, branchId, "ORD-ERR-01", 999.00m);
                    dbContext.Orders.Add(order);

                    var domainEvent = new OrderPlacedEvent
                    {
                        EventId = eventId,
                        AggregateId = orderId,
                        TenantId = tenantId,
                        BranchId = branchId,
                        DeviceId = deviceId,
                        OrderNumber = "ORD-ERR-01",
                        TotalAmount = 999.00m
                    };

                    var outboxMessage = OutboxMessage.FromEvent(domainEvent, 1, "{\"orderNumber\":\"ORD-ERR-01\"}");
                    await repository.AddAsync(outboxMessage);

                    await unitOfWork.SaveChangesAsync();

                    // Unexpected application crash / unhandled failure before transaction.CommitAsync()
                    throw new InvalidOperationException("Simulated catastrophic application crash during checkout processing!");
                }
            });
        }

        // Assert - Verify using a fresh DbContext instance that neither business mutation nor outbox message was persisted
        using var verifyContext = new LocalPosDbContext(_options);
        var persistedOrder = await verifyContext.Orders.FindAsync(orderId);
        var persistedOutbox = await verifyContext.OutboxMessages.FindAsync(eventId);

        Assert.Null(persistedOrder);
        Assert.Null(persistedOutbox);
    }
}
