namespace Yashdeep.Tests;

using System.Text.Json;
using Yashdeep.Application.Pos.DTOs;
using Yashdeep.Application.Pos.UI;
using Yashdeep.Application.Pos.Workflows;
using Yashdeep.Domain.Entities.Billing;
using Yashdeep.Domain.Entities.Orders;
using Yashdeep.Domain.Entities.Sync;
using Yashdeep.Domain.ValueObjects;
using Yashdeep.Infrastructure.Persistence;
using Yashdeep.Infrastructure.Printing;
using Yashdeep.Infrastructure.SyncEngine;
using Xunit;

public class PosVerticalSliceTests
{
    private readonly LocalPosMemoryDbContext _dbContext;
    private readonly LocalPosUnitOfWork _unitOfWork;
    private readonly TestPrinterService _printerService;
    private readonly CloudInboxProcessor _inboxProcessor;
    private readonly CloudSyncEngine _syncEngine;
    private readonly CompletePosWorkflowUseCase _workflowUseCase;
    private readonly PosTerminalUiController _uiController;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Guid _deviceId = Guid.NewGuid();

    public PosVerticalSliceTests()
    {
        _dbContext = new LocalPosMemoryDbContext();
        _unitOfWork = new LocalPosUnitOfWork(_dbContext);
        _printerService = new TestPrinterService();
        _inboxProcessor = new CloudInboxProcessor();
        _syncEngine = new CloudSyncEngine(_unitOfWork.Outbox, _inboxProcessor);
        _workflowUseCase = new CompletePosWorkflowUseCase(_unitOfWork, _printerService, _syncEngine);

        _uiController = new PosTerminalUiController(_workflowUseCase)
        {
            TenantId = _tenantId,
            BranchId = _branchId,
            LocationId = _locationId,
            DeviceId = _deviceId,
            ActiveTableNumber = "T-12",
            ActiveSection = SectionTier.Ac,
            ActiveBusinessDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CaptainUserId = Guid.NewGuid(),
            WaiterName = "Captain Suresh",
            CashierUserId = "CASHIER_01",
            DiscountPercentage = 10m // 10% discount test
        };
    }

    [Fact]
    public void Domain_Money_OperatorsAndRounding_BehaveWithExactPrecision()
    {
        // Arrange
        var m1 = new Money(100.255m, "INR");
        var m2 = new Money(50.345m, "INR");

        // Act & Assert
        Assert.Equal(150.60m, (m1 + m2).Amount);
        Assert.Equal(49.91m, (m1 - m2).Amount);
        Assert.Equal(100.26m, m1.Round().Amount); // AwayFromZero
        Assert.Equal(50.35m, m2.Round().Amount);  // AwayFromZero
        Assert.Equal(100m, m1.RoundToNearestRupee().Amount);
    }

    [Fact]
    public void Domain_Bill_TaxCalculationPolicy_ComputesCgstSgstCorrectly()
    {
        // Arrange
        var foodSub = new Money(500m);
        var liquorSub = new Money(0m);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act - Food ₹500, 0% discount, 2.5% CGST + 2.5% SGST (5% total = ₹25 tax)
        var bill = new Bill(
            Guid.NewGuid(),
            _tenantId,
            _branchId,
            _locationId,
            Guid.NewGuid(),
            today,
            "INV-001",
            1,
            "T-01",
            "Waiter 1",
            foodSub,
            liquorSub,
            discountPercentage: 0m,
            foodCgstPercent: 2.5m,
            foodSgstPercent: 2.5m
        );

        // Assert
        Assert.Equal(500m, bill.SubTotal.Amount);
        Assert.Equal(0m, bill.TotalDiscount.Amount);
        Assert.Equal(25m, bill.TotalTax.Amount);
        Assert.Equal(525m, bill.GrandTotal.Amount);
        Assert.Equal(2, bill.TaxLines.Count);
        Assert.Contains(bill.TaxLines, t => t.TaxName == "CGST" && t.TaxAmount.Amount == 12.50m);
        Assert.Contains(bill.TaxLines, t => t.TaxName == "SGST" && t.TaxAmount.Amount == 12.50m);
    }

    [Fact]
    public void Domain_Order_BilingualMarathiItem_GeneratesKotWithMarathiNames()
    {
        // Arrange
        var order = new Order(
            Guid.NewGuid(),
            _tenantId,
            _branchId,
            _locationId,
            "T-05",
            SectionTier.Family,
            OrderType.DineIn,
            "ORD-101",
            DateOnly.FromDateTime(DateTime.UtcNow),
            Guid.NewGuid(),
            "Captain Vikas"
        );

        order.AddItem(
            Guid.NewGuid(),
            "101",
            "Chicken Tikka",
            "चिकन टिक्का",
            DepartmentType.Kitchen,
            2,
            new Money(250m)
        );

        // Act
        var kot = order.GenerateKot(KotTicketType.KotKitchen, "KOT-001");

        // Assert
        Assert.NotNull(kot);
        Assert.Single(kot.LineItems);
        var line = kot.LineItems.First();
        Assert.Equal("Chicken Tikka", line.EnglishName);
        Assert.Equal("चिकन टिक्का", line.MarathiName);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public async Task OfflineIntegration_WorkflowCompletesOffline_PersistsAtomically_CreatesOutbox_AndSyncsWhenOnline()
    {
        // 1. Simulate Offline Mode
        _syncEngine.SetNetworkAvailable(false);

        // Add items via keyboard/touch UI controller
        Guid itemFoodId = Guid.NewGuid();
        Guid itemBarId = Guid.NewGuid();

        _uiController.AddItemToCart(
            itemFoodId,
            "101",
            "Butter Chicken",
            "बटर चिकन",
            DepartmentType.Kitchen,
            quantity: 2,
            unitPrice: 350m
        );

        _uiController.AddItemToCart(
            itemBarId,
            "501",
            "Kingfisher Beer 650ml",
            "किंगफिशर बीअर",
            DepartmentType.Bar,
            quantity: 1,
            unitPrice: 220m,
            unitVolumeMl: 650
        );

        // Split Payment: ₹500 Cash + ₹420 UPI
        var payments = new List<ProcessPaymentRequest>
        {
            new(PaymentMethod.Cash, 500m, "CASH_REF_01"),
            new(PaymentMethod.UpiQr, 420m, "UPI_RRN_9988776655")
        };

        // Act 1: Process workflow offline
        var result = await _uiController.ProcessCheckoutAndSettleAsync(payments);

        // Assert 1: Local POS Workflow Execution Completed Offline
        Assert.True(result.OfflineSaved);
        Assert.False(result.CloudSynced, "Should NOT sync to cloud while offline.");
        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
        Assert.Equal(2, result.KotIds.Count); // 1 Kitchen KOT + 1 Bar BOT
        Assert.Equal(2, result.StockMovementIds.Count);
        Assert.Single(result.OutboxEventIds);

        // Verify Local Durable Atomic Persistence
        var persistedOrder = await _unitOfWork.Orders.GetByIdAsync(result.OrderId, _tenantId);
        Assert.NotNull(persistedOrder);
        Assert.Equal(OrderStatus.Completed, persistedOrder.Status);

        var persistedBill = await _unitOfWork.Bills.GetByIdAsync(result.BillId, _tenantId);
        Assert.NotNull(persistedBill);
        Assert.Equal(2, persistedBill.Payments.Count);

        var persistedMovements = await _unitOfWork.Stock.GetMovementsByReferenceAsync(result.BillId, _tenantId);
        Assert.Equal(2, persistedMovements.Count);

        var persistedAudits = await _unitOfWork.Audits.GetEventsByReferenceAsync(result.BillId, _tenantId);
        Assert.Single(persistedAudits);

        var pendingOutbox = await _unitOfWork.Outbox.GetPendingMessagesAsync(_tenantId);
        Assert.Single(pendingOutbox);
        var outboxMsg = pendingOutbox.First();
        Assert.Equal(OutboxMessageStatus.Pending, outboxMsg.Status);
        Assert.False(string.IsNullOrWhiteSpace(outboxMsg.PayloadHash));

        // Verify Printer Service received receipt streams
        Assert.Equal(2, _printerService.PrintedKotReceipts.Count);
        Assert.Single(_printerService.PrintedBillReceipts);

        // Act 2: Re-establish Network Connection & Synchronize Pending Outbox
        _syncEngine.SetNetworkAvailable(true);
        int syncedCount = await _syncEngine.SynchronizePendingOutboxAsync(_tenantId);

        // Assert 2: Synchronization Success
        Assert.Equal(1, syncedCount);
        var pendingOutboxAfterSync = await _unitOfWork.Outbox.GetPendingMessagesAsync(_tenantId);
        Assert.Empty(pendingOutboxAfterSync); // Mark uploaded
        Assert.True(_inboxProcessor.HasMessage(_tenantId, outboxMsg.EventId));

        // Act 3: Attempt Repeated Duplicate Synchronization (Idempotency Check)
        var (accepted, isDuplicate) = _inboxProcessor.ProcessInboxMessage(outboxMsg);

        // Assert 3: Idempotent Replay Handling
        Assert.True(accepted, "Duplicate payload ACKed successfully.");
        Assert.True(isDuplicate, "Duplicate event flagged correctly.");
        Assert.Equal(1, _inboxProcessor.GetProcessedMessageCount(_tenantId)); // Count remains 1, no duplicate inserted
    }

    [Fact]
    public async Task Security_TenantIsolation_EnforcesStrictDataSeparation()
    {
        // Arrange
        Guid otherTenantId = Guid.NewGuid();

        _uiController.AddItemToCart(
            Guid.NewGuid(),
            "201",
            "Paneer Tikka",
            "पनीर टिक्का",
            DepartmentType.Kitchen,
            quantity: 1,
            unitPrice: 200m
        );

        var payments = new List<ProcessPaymentRequest>
        {
            new(PaymentMethod.Cash, 200m, "CASH_REF_02")
        };

        var result = await _uiController.ProcessCheckoutAndSettleAsync(payments);

        // Act - Query data belonging to _tenantId using otherTenantId
        var orderForOtherTenant = await _unitOfWork.Orders.GetByIdAsync(result.OrderId, otherTenantId);
        var billForOtherTenant = await _unitOfWork.Bills.GetByIdAsync(result.BillId, otherTenantId);
        var movementsForOtherTenant = await _unitOfWork.Stock.GetMovementsByReferenceAsync(result.BillId, otherTenantId);
        var auditsForOtherTenant = await _unitOfWork.Audits.GetEventsByReferenceAsync(result.BillId, otherTenantId);

        // Assert - 0 records returned for cross-tenant query
        Assert.Null(orderForOtherTenant);
        Assert.Null(billForOtherTenant);
        Assert.Empty(movementsForOtherTenant);
        Assert.Empty(auditsForOtherTenant);
    }
}
