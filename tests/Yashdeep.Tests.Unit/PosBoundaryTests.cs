using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Application.Pos.DTOs;
using Yashdeep.Application.Pos.Services;
using Yashdeep.Domain.Entities.Audit;
using Yashdeep.Domain.Entities.Billing;
using Yashdeep.Domain.Entities.Inventory;
using Yashdeep.Domain.Entities.Orders;
using Yashdeep.Domain.Entities.Sync;
using Yashdeep.Domain.ValueObjects;
using Yashdeep.Server.Api.Controllers;
using Yashdeep.Shared.Contracts;
using Yashdeep.Shared.Results;

namespace Yashdeep.Tests.Unit;

public class PosBoundaryTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<IBillRepository> _billRepoMock = new();
    private readonly Mock<IStockRepository> _stockRepoMock = new();
    private readonly Mock<IAuditRepository> _auditRepoMock = new();
    private readonly Mock<IOutboxRepository> _outboxRepoMock = new();
    private readonly Mock<ILocalPosUnitOfWork> _uowMock = new();
    private readonly Mock<IPrinterService> _printerMock = new();

    private readonly PosApplicationService _service;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    public PosBoundaryTests()
    {
        _uowMock.Setup(u => u.Orders).Returns(_orderRepoMock.Object);
        _uowMock.Setup(u => u.Bills).Returns(_billRepoMock.Object);
        _uowMock.Setup(u => u.Stock).Returns(_stockRepoMock.Object);
        _uowMock.Setup(u => u.Audits).Returns(_auditRepoMock.Object);
        _uowMock.Setup(u => u.Outbox).Returns(_outboxRepoMock.Object);
        _uowMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _service = new PosApplicationService(_uowMock.Object, _printerMock.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_ValidRequest_CreatesOrderAndCommits()
    {
        var request = new CreateOrderRequest(
            _locationId,
            "T-10",
            SectionTier.AC,
            OrderType.DineIn,
            "ORD-001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            Guid.NewGuid(),
            "Ramesh",
            new List<PosOrderItemRequest>
            {
                new(Guid.NewGuid(), "F101", "Paneer Butter Masala", "पनीर बटर मसाला", DepartmentType.Kitchen, 2, 250m)
            }
        );

        var result = await _service.CreateOrderAsync(_tenantId, _branchId, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("T-10", result.Value.TableNumber);
        Assert.Equal(1, result.Value.ItemCount);
        _orderRepoMock.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddOrderItemAsync_BranchMismatch_ReturnsValidationError()
    {
        var orderId = Guid.NewGuid();
        var otherBranchId = Guid.NewGuid();
        var order = new Order(orderId, _tenantId, otherBranchId, _locationId, "T-1", SectionTier.MainHall, OrderType.DineIn, "ORD-1", DateOnly.FromDateTime(DateTime.UtcNow), Guid.NewGuid(), "Waiter");

        _orderRepoMock.Setup(r => r.GetByIdAsync(orderId, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var request = new AddOrderItemRequest(Guid.NewGuid(), "BEV1", "Coke", "कोक", DepartmentType.Beverage, 1, 40m);

        var result = await _service.AddOrderItemAsync(_tenantId, _branchId, orderId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("POS.BRANCH_MISMATCH", result.Error.Code);
    }

    [Fact]
    public async Task GenerateKotAsync_ValidOrder_GeneratesKotAndPrints()
    {
        var orderId = Guid.NewGuid();
        var order = new Order(orderId, _tenantId, _branchId, _locationId, "T-2", SectionTier.MainHall, OrderType.DineIn, "ORD-2", DateOnly.FromDateTime(DateTime.UtcNow), Guid.NewGuid(), "Waiter");
        order.AddItem(Guid.NewGuid(), "F1", "Dal Tadka", "डाळ तडका", DepartmentType.Kitchen, 1, new Money(180m));

        _orderRepoMock.Setup(r => r.GetByIdAsync(orderId, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var result = await _service.GenerateKotAsync(_tenantId, _branchId, orderId, new GenerateKotRequest(KotTicketType.KotKitchen, "KOT-101"));

        Assert.True(result.IsSuccess);
        Assert.Equal("KOT-101", result.Value!.KotNumber);
        _printerMock.Verify(p => p.PrintKotAsync(It.IsAny<KotRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateBillAsync_ValidOrder_GeneratesBillAndCalculatesTotals()
    {
        var orderId = Guid.NewGuid();
        var order = new Order(orderId, _tenantId, _branchId, _locationId, "T-3", SectionTier.Bar, OrderType.DineIn, "ORD-3", DateOnly.FromDateTime(DateTime.UtcNow), Guid.NewGuid(), "Waiter");
        order.AddItem(Guid.NewGuid(), "L1", "Whisky 60ml", "व्हिस्की 60ml", DepartmentType.Bar, 2, new Money(300m));

        _orderRepoMock.Setup(r => r.GetByIdAsync(orderId, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _billRepoMock.Setup(r => r.GetNextDailySequenceNumberAsync(_tenantId, _branchId, order.BusinessDate, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.GenerateBillAsync(_tenantId, _branchId, orderId, new GenerateBillRequest(10m));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Unpaid, result.Value!.Status);
        _billRepoMock.Verify(b => b.AddAsync(It.IsAny<Bill>(), It.IsAny<CancellationToken>()), Times.Once);
        _printerMock.Verify(p => p.PrintReceiptAsync(It.IsAny<Bill>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordPaymentAsync_AlreadyPaidBill_ReturnsConflictError()
    {
        var billId = Guid.NewGuid();
        var bill = new Bill(_tenantId, _branchId, _locationId, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "INV-001", 1, "T-1", "W1", new Money(100m), Money.Zero, billId: billId);
        bill.AddPayment(new Payment(Guid.NewGuid(), bill.Id, PaymentMethod.Cash, bill.GrandTotal, "REF1"));

        _billRepoMock.Setup(b => b.GetByIdAsync(billId, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(bill);

        var result = await _service.RecordPaymentAsync(_tenantId, _branchId, billId, new RecordPaymentRequest(PaymentMethod.Upi, 100m, "REF2"));

        Assert.False(result.IsSuccess);
        Assert.Equal("POS.BILL_ALREADY_PAID", result.Error.Code);
    }

    [Fact]
    public async Task RecordStockMovementAsync_ValidRequest_AddsMovement()
    {
        var request = new RecordStockMovementRequest(
            Guid.NewGuid(), "F101", "Paneer", DepartmentType.Kitchen, StockMovementType.Consumption,
            0.5m, "KG", _locationId, Guid.NewGuid(), "ORDER", "Order item consumed"
        );

        var result = await _service.RecordStockMovementAsync(_tenantId, _branchId, request);

        Assert.True(result.IsSuccess);
        _stockRepoMock.Verify(s => s.AddMovementAsync(It.IsAny<StockMovement>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PersistAuditAsync_ValidRequest_StoresAudit()
    {
        var request = new PersistAuditRequest(
            Guid.NewGuid(), Guid.NewGuid(), "POS_ORDER_CREATED", "Order", Guid.NewGuid(), "Order created on terminal", "127.0.0.1"
        );

        var result = await _service.PersistAuditAsync(_tenantId, _branchId, request);

        Assert.True(result.IsSuccess);
        _auditRepoMock.Verify(a => a.AddEventAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueueSyncEventAsync_ValidPayload_QueuesOutboxMessage()
    {
        var request = new QueueSyncEventRequest(
            Guid.NewGuid(), Guid.NewGuid(), "OrderCreatedEvent", 1, "{\"OrderId\":\"123\"}"
        );

        var result = await _service.QueueSyncEventAsync(_tenantId, _branchId, request);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!.PayloadHash);
        _outboxRepoMock.Verify(o => o.AddMessageAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PosController_CreateOrder_MissingTenantContext_ReturnsUnauthorized()
    {
        var serviceMock = new Mock<IPosApplicationService>();
        var controller = new PosController(serviceMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new CreateOrderRequest(_locationId, "T-1", SectionTier.AC, OrderType.DineIn, "O-1", DateOnly.FromDateTime(DateTime.UtcNow), Guid.NewGuid(), "W");

        var response = await controller.CreateOrder(request, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(response);
        var apiResp = Assert.IsType<ApiResponse<object>>(unauthorizedResult.Value);
        Assert.False(apiResp.Success);
        Assert.Equal("AUTH.MISSING_TENANT", apiResp.Error!.Code);
    }

    [Fact]
    public async Task PosController_CreateOrder_ValidContext_DelegatesToService()
    {
        var serviceMock = new Mock<IPosApplicationService>();
        var expectedResponse = new PosOrderResponse(
            Guid.NewGuid(), _tenantId, _branchId, _locationId, "T-1", SectionTier.AC, OrderType.DineIn,
            OrderStatus.Open, "O-1", DateOnly.FromDateTime(DateTime.UtcNow), Guid.NewGuid(), "W", Money.Zero, 0
        );

        serviceMock.Setup(s => s.CreateOrderAsync(_tenantId, _branchId, It.IsAny<CreateOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PosOrderResponse>.Success(expectedResponse));

        var controller = new PosController(serviceMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = _tenantId;
        httpContext.Items["BranchId"] = _branchId;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new CreateOrderRequest(_locationId, "T-1", SectionTier.AC, OrderType.DineIn, "O-1", DateOnly.FromDateTime(DateTime.UtcNow), Guid.NewGuid(), "W");

        var response = await controller.CreateOrder(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        var apiResp = Assert.IsType<ApiResponse<PosOrderResponse>>(okResult.Value);
        Assert.True(apiResp.Success);
        Assert.Equal("T-1", apiResp.Data!.TableNumber);
    }
}
