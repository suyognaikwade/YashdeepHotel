using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Domain.Entities.Audit;
using Yashdeep.Domain.Entities.Billing;
using Yashdeep.Domain.Entities.Inventory;
using Yashdeep.Domain.Entities.Orders;
using Yashdeep.Domain.Entities.Sync;
using Yashdeep.Domain.Outbox;
using Yashdeep.Domain.ValueObjects;
using Yashdeep.Shared.Results;

namespace Yashdeep.Application.Pos.Services;

public class PosApplicationService : IPosApplicationService
{
    private readonly ILocalPosUnitOfWork _unitOfWork;
    private readonly IPrinterService? _printerService;

    public PosApplicationService(ILocalPosUnitOfWork unitOfWork, IPrinterService? printerService = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _printerService = printerService;
    }

    public async Task<Result<PosOrderResponse>> CreateOrderAsync(
        Guid tenantId,
        Guid branchId,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result<PosOrderResponse>.Failure(Error.Validation("POS.INVALID_TENANT", "Tenant ID must be provided."));
        if (branchId == Guid.Empty)
            return Result<PosOrderResponse>.Failure(Error.Validation("POS.INVALID_BRANCH", "Branch ID must be provided."));
        if (string.IsNullOrWhiteSpace(request.TableNumber))
            return Result<PosOrderResponse>.Failure(Error.Validation("POS.INVALID_TABLE", "Table number must be provided."));

        var order = new Order(
            Guid.NewGuid(),
            tenantId,
            branchId,
            request.LocationId,
            request.TableNumber,
            request.Section,
            request.OrderType,
            request.OrderNumber,
            request.BusinessDate,
            request.CaptainUserId,
            request.WaiterName
        );

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var item in request.Items)
            {
                order.AddItem(
                    item.MenuItemId,
                    item.ItemCode,
                    item.EnglishName,
                    item.MarathiName,
                    item.Department,
                    item.Quantity,
                    new Money(item.UnitPrice),
                    item.UnitVolumeMl
                );
            }
        }

        await _unitOfWork.Orders.AddAsync(order, cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        return Result<PosOrderResponse>.Success(MapOrderResponse(order));
    }

    public async Task<Result<PosOrderResponse>> AddOrderItemAsync(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        AddOrderItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId, tenantId, cancellationToken);
        if (order == null)
            return Result<PosOrderResponse>.Failure(Error.NotFound("POS.ORDER_NOT_FOUND", $"Order with ID {orderId} was not found."));

        if (order.BranchId != branchId)
            return Result<PosOrderResponse>.Failure(Error.Validation("POS.BRANCH_MISMATCH", "Branch ID mismatch. Access denied."));

        if (request.Quantity <= 0)
            return Result<PosOrderResponse>.Failure(Error.Validation("POS.INVALID_QUANTITY", "Quantity must be greater than zero."));

        try
        {
            order.AddItem(
                request.MenuItemId,
                request.ItemCode,
                request.EnglishName,
                request.MarathiName,
                request.Department,
                request.Quantity,
                new Money(request.UnitPrice),
                request.UnitVolumeMl
            );

            await _unitOfWork.Orders.UpdateAsync(order, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result<PosOrderResponse>.Success(MapOrderResponse(order));
        }
        catch (InvalidOperationException ex)
        {
            return Result<PosOrderResponse>.Failure(Error.Conflict("POS.ORDER_STATE_INVALID", ex.Message));
        }
    }

    public async Task<Result<PosKotResponse>> GenerateKotAsync(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        GenerateKotRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId, tenantId, cancellationToken);
        if (order == null)
            return Result<PosKotResponse>.Failure(Error.NotFound("POS.ORDER_NOT_FOUND", $"Order with ID {orderId} was not found."));

        if (order.BranchId != branchId)
            return Result<PosKotResponse>.Failure(Error.Validation("POS.BRANCH_MISMATCH", "Branch ID mismatch. Access denied."));

        try
        {
            var kot = order.GenerateKot(request.TicketType, request.KotNumber);
            await _unitOfWork.Orders.UpdateAsync(order, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            if (_printerService != null)
            {
                await _printerService.PrintKotAsync(kot, cancellationToken);
            }

            return Result<PosKotResponse>.Success(new PosKotResponse(
                kot.Id,
                kot.OrderId,
                kot.KotNumber,
                kot.TicketType,
                kot.LineItems.Count
            ));
        }
        catch (InvalidOperationException ex)
        {
            return Result<PosKotResponse>.Failure(Error.Conflict("POS.KOT_GENERATION_FAILED", ex.Message));
        }
    }

    public async Task<Result<PosBillResponse>> GenerateBillAsync(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        GenerateBillRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId, tenantId, cancellationToken);
        if (order == null)
            return Result<PosBillResponse>.Failure(Error.NotFound("POS.ORDER_NOT_FOUND", $"Order with ID {orderId} was not found."));

        if (order.BranchId != branchId)
            return Result<PosBillResponse>.Failure(Error.Validation("POS.BRANCH_MISMATCH", "Branch ID mismatch. Access denied."));

        var nextSeq = await _unitOfWork.Bills.GetNextDailySequenceNumberAsync(
            tenantId,
            branchId,
            order.BusinessDate,
            cancellationToken
        );

        string invoiceNumber = $"INV-{order.BusinessDate:yyyyMMdd}-{nextSeq:D4}";

        var bill = new Bill(
            Guid.NewGuid(),
            tenantId,
            branchId,
            order.LocationId,
            order.Id,
            order.BusinessDate,
            invoiceNumber,
            nextSeq,
            order.TableNumber,
            order.WaiterName,
            order.CalculateFoodSubTotal(),
            order.CalculateLiquorSubTotal(),
            request.DiscountPercentage
        );

        order.MarkBilled();

        await _unitOfWork.Bills.AddAsync(bill, cancellationToken);
        await _unitOfWork.Orders.UpdateAsync(order, cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        if (_printerService != null)
        {
            await _printerService.PrintReceiptAsync(bill, cancellationToken);
        }

        return Result<PosBillResponse>.Success(MapBillResponse(bill));
    }

    public async Task<Result<PosBillResponse>> RecordPaymentAsync(
        Guid tenantId,
        Guid branchId,
        Guid billId,
        RecordPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var bill = await _unitOfWork.Bills.GetByIdAsync(billId, tenantId, cancellationToken);
        if (bill == null)
            return Result<PosBillResponse>.Failure(Error.NotFound("POS.BILL_NOT_FOUND", $"Bill with ID {billId} was not found."));

        if (bill.BranchId != branchId)
            return Result<PosBillResponse>.Failure(Error.Validation("POS.BRANCH_MISMATCH", "Branch ID mismatch. Access denied."));

        if (bill.PaymentStatus == PaymentStatus.Paid)
            return Result<PosBillResponse>.Failure(Error.Conflict("POS.BILL_ALREADY_PAID", "Bill has already been fully paid."));

        var payment = new Payment(
            Guid.NewGuid(),
            bill.Id,
            request.Method,
            new Money(request.Amount),
            request.TransactionReference
        );

        bill.AddPayment(payment);

        await _unitOfWork.Bills.UpdateAsync(bill, cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        return Result<PosBillResponse>.Success(MapBillResponse(bill));
    }

    public async Task<Result<PosOrderResponse>> CompleteOrderAsync(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId, tenantId, cancellationToken);
        if (order == null)
            return Result<PosOrderResponse>.Failure(Error.NotFound("POS.ORDER_NOT_FOUND", $"Order with ID {orderId} was not found."));

        if (order.BranchId != branchId)
            return Result<PosOrderResponse>.Failure(Error.Validation("POS.BRANCH_MISMATCH", "Branch ID mismatch. Access denied."));

        try
        {
            order.MarkCompleted();
            await _unitOfWork.Orders.UpdateAsync(order, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result<PosOrderResponse>.Success(MapOrderResponse(order));
        }
        catch (InvalidOperationException ex)
        {
            return Result<PosOrderResponse>.Failure(Error.Conflict("POS.ORDER_STATE_INVALID", ex.Message));
        }
    }

    public async Task<Result<PosStockMovementResponse>> RecordStockMovementAsync(
        Guid tenantId,
        Guid branchId,
        RecordStockMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result<PosStockMovementResponse>.Failure(Error.Validation("POS.INVALID_TENANT", "Tenant ID must be provided."));
        if (branchId == Guid.Empty)
            return Result<PosStockMovementResponse>.Failure(Error.Validation("POS.INVALID_BRANCH", "Branch ID must be provided."));
        if (request.Quantity <= 0)
            return Result<PosStockMovementResponse>.Failure(Error.Validation("POS.INVALID_QUANTITY", "Movement quantity must be greater than zero."));

        var movement = new StockMovement(
            Guid.NewGuid(),
            tenantId,
            branchId,
            request.LocationId,
            request.MenuItemId,
            request.ItemCode,
            request.ItemName,
            request.Department,
            request.MovementType,
            request.Quantity,
            request.UnitOfMeasure,
            request.ReferenceId,
            request.ReferenceType,
            request.Reason
        );

        await _unitOfWork.Stock.AddMovementAsync(movement, cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        return Result<PosStockMovementResponse>.Success(new PosStockMovementResponse(
            movement.Id,
            movement.TenantId,
            movement.BranchId,
            movement.MenuItemId,
            movement.Quantity,
            movement.MovementType
        ));
    }

    public async Task<Result<PosAuditResponse>> PersistAuditAsync(
        Guid tenantId,
        Guid branchId,
        PersistAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result<PosAuditResponse>.Failure(Error.Validation("POS.INVALID_TENANT", "Tenant ID must be provided."));
        if (branchId == Guid.Empty)
            return Result<PosAuditResponse>.Failure(Error.Validation("POS.INVALID_BRANCH", "Branch ID must be provided."));

        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            tenantId,
            branchId,
            request.DeviceId,
            request.UserId,
            request.EventType,
            request.AggregateType,
            request.AggregateId,
            request.Details,
            request.IpAddress
        );

        await _unitOfWork.Audits.AddEventAsync(auditEvent, cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        return Result<PosAuditResponse>.Success(new PosAuditResponse(
            auditEvent.Id,
            auditEvent.TenantId,
            auditEvent.BranchId,
            auditEvent.EventType,
            auditEvent.TimestampUtc
        ));
    }

    public async Task<Result<PosSyncQueueResponse>> QueueSyncEventAsync(
        Guid tenantId,
        Guid branchId,
        QueueSyncEventRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result<PosSyncQueueResponse>.Failure(Error.Validation("POS.INVALID_TENANT", "Tenant ID must be provided."));
        if (branchId == Guid.Empty)
            return Result<PosSyncQueueResponse>.Failure(Error.Validation("POS.INVALID_BRANCH", "Branch ID must be provided."));
        if (string.IsNullOrWhiteSpace(request.PayloadJson))
            return Result<PosSyncQueueResponse>.Failure(Error.Validation("POS.INVALID_PAYLOAD", "Payload JSON must not be empty."));

        string payloadHash = PayloadHasher.ComputeSha256Hash(request.PayloadJson);

        var message = new OutboxMessage(
            Guid.NewGuid(),
            request.AggregateId,
            tenantId,
            branchId,
            request.DeviceId,
            0,
            request.EventType,
            request.EventVersion,
            request.PayloadJson,
            payloadHash
        );

        await _unitOfWork.Outbox.AddMessageAsync(message, cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        return Result<PosSyncQueueResponse>.Success(new PosSyncQueueResponse(
            message.EventId,
            message.TenantId,
            message.BranchId,
            message.EventType,
            message.PayloadHash
        ));
    }

    private static PosOrderResponse MapOrderResponse(Order order)
    {
        return new PosOrderResponse(
            order.Id,
            order.TenantId,
            order.BranchId,
            order.LocationId,
            order.TableNumber,
            order.Section,
            order.OrderType,
            order.Status,
            order.OrderNumber,
            order.BusinessDate,
            order.CaptainUserId,
            order.WaiterName,
            order.CalculateSubTotal(),
            order.Items.Count
        );
    }

    private static PosBillResponse MapBillResponse(Bill bill)
    {
        return new PosBillResponse(
            bill.Id,
            bill.OrderId,
            bill.InvoiceNumber,
            bill.DailySequenceNumber,
            bill.SubTotal,
            bill.TotalTax,
            bill.TotalDiscount,
            bill.GrandTotal,
            bill.TotalPaid,
            bill.PaymentStatus
        );
    }
}
