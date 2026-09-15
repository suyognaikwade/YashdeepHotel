using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Application.Pos.DTOs;
using Yashdeep.Domain.ValueObjects;
using Yashdeep.Shared.Results;

namespace Yashdeep.Application.Pos.Services;

public record CreateOrderRequest(
    Guid LocationId,
    string TableNumber,
    SectionTier Section,
    OrderType OrderType,
    string OrderNumber,
    DateOnly BusinessDate,
    Guid CaptainUserId,
    string WaiterName,
    List<PosOrderItemRequest>? Items = null
);

public record AddOrderItemRequest(
    Guid MenuItemId,
    string ItemCode,
    string EnglishName,
    string MarathiName,
    DepartmentType Department,
    decimal Quantity,
    decimal UnitPrice,
    int? UnitVolumeMl = null
);

public record GenerateKotRequest(
    KotTicketType TicketType,
    string KotNumber
);

public record GenerateBillRequest(
    decimal DiscountPercentage = 0
);

public record RecordPaymentRequest(
    PaymentMethod Method,
    decimal Amount,
    string TransactionReference
);

public record RecordStockMovementRequest(
    Guid MenuItemId,
    string ItemCode,
    string ItemName,
    DepartmentType Department,
    StockMovementType MovementType,
    decimal Quantity,
    string UnitOfMeasure,
    Guid LocationId,
    Guid ReferenceId,
    string ReferenceType,
    string Reason
);

public record PersistAuditRequest(
    Guid DeviceId,
    Guid UserId,
    string EventType,
    string AggregateType,
    Guid AggregateId,
    string Details,
    string IpAddress
);

public record QueueSyncEventRequest(
    Guid AggregateId,
    Guid DeviceId,
    string EventType,
    int EventVersion,
    string PayloadJson
);

public record PosOrderResponse(
    Guid OrderId,
    Guid TenantId,
    Guid BranchId,
    Guid LocationId,
    string TableNumber,
    SectionTier Section,
    OrderType OrderType,
    OrderStatus Status,
    string OrderNumber,
    DateOnly BusinessDate,
    Guid CaptainUserId,
    string WaiterName,
    Money SubTotal,
    int ItemCount
);

public record PosKotResponse(
    Guid KotId,
    Guid OrderId,
    string KotNumber,
    KotTicketType TicketType,
    int ItemCount
);

public record PosBillResponse(
    Guid BillId,
    Guid OrderId,
    string InvoiceNumber,
    long DailySequenceNumber,
    Money SubTotal,
    Money TotalTax,
    Money TotalDiscount,
    Money GrandTotal,
    Money TotalPaid,
    PaymentStatus Status
);

public record PosStockMovementResponse(
    Guid MovementId,
    Guid TenantId,
    Guid BranchId,
    Guid MenuItemId,
    decimal Quantity,
    StockMovementType MovementType
);

public record PosAuditResponse(
    Guid AuditEventId,
    Guid TenantId,
    Guid BranchId,
    string EventType,
    DateTime TimestampUtc
);

public record PosSyncQueueResponse(
    Guid EventId,
    Guid TenantId,
    Guid BranchId,
    string EventType,
    string PayloadHash
);

public interface IPosApplicationService
{
    Task<Result<PosOrderResponse>> CreateOrderAsync(Guid tenantId, Guid branchId, CreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<Result<PosOrderResponse>> AddOrderItemAsync(Guid tenantId, Guid branchId, Guid orderId, AddOrderItemRequest request, CancellationToken cancellationToken = default);
    Task<Result<PosKotResponse>> GenerateKotAsync(Guid tenantId, Guid branchId, Guid orderId, GenerateKotRequest request, CancellationToken cancellationToken = default);
    Task<Result<PosBillResponse>> GenerateBillAsync(Guid tenantId, Guid branchId, Guid orderId, GenerateBillRequest request, CancellationToken cancellationToken = default);
    Task<Result<PosBillResponse>> RecordPaymentAsync(Guid tenantId, Guid branchId, Guid billId, RecordPaymentRequest request, CancellationToken cancellationToken = default);
    Task<Result<PosOrderResponse>> CompleteOrderAsync(Guid tenantId, Guid branchId, Guid orderId, CancellationToken cancellationToken = default);
    Task<Result<PosStockMovementResponse>> RecordStockMovementAsync(Guid tenantId, Guid branchId, RecordStockMovementRequest request, CancellationToken cancellationToken = default);
    Task<Result<PosAuditResponse>> PersistAuditAsync(Guid tenantId, Guid branchId, PersistAuditRequest request, CancellationToken cancellationToken = default);
    Task<Result<PosSyncQueueResponse>> QueueSyncEventAsync(Guid tenantId, Guid branchId, QueueSyncEventRequest request, CancellationToken cancellationToken = default);
}
