using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Application.Pos.DTOs;

public record PosOrderItemRequest(
    Guid MenuItemId,
    string ItemCode,
    string EnglishName,
    string MarathiName,
    DepartmentType Department,
    decimal Quantity,
    decimal UnitPrice,
    int? UnitVolumeMl = null
);

public record CreatePosOrderCommand(
    Guid TenantId,
    Guid BranchId,
    Guid LocationId,
    string TableNumber,
    SectionTier Section,
    OrderType OrderType,
    string OrderNumber,
    DateOnly BusinessDate,
    Guid CaptainUserId,
    string WaiterName,
    List<PosOrderItemRequest> Items
);

public record ProcessPaymentRequest(
    PaymentMethod Method,
    decimal Amount,
    string TransactionReference
);

public record PosTaxPolicyOptions(
    decimal FoodCgstPercent = 2.5m,
    decimal FoodSgstPercent = 2.5m,
    decimal LiquorVatPercent = 0m
);

public record CompletePosWorkflowCommand(
    Guid TenantId,
    Guid BranchId,
    Guid LocationId,
    Guid DeviceId,
    string TableNumber,
    SectionTier Section,
    OrderType OrderType,
    string OrderNumber,
    DateOnly BusinessDate,
    Guid CaptainUserId,
    string WaiterName,
    string CashierUserId,
    List<PosOrderItemRequest> Items,
    decimal DiscountPercentage,
    List<ProcessPaymentRequest> Payments,
    PosTaxPolicyOptions? TaxPolicy = null,
    bool AllowOverpayment = false
);

public record PosWorkflowResult(
    Guid OrderId,
    Guid BillId,
    string InvoiceNumber,
    long DailySequenceNumber,
    Money GrandTotal,
    Money TotalPaid,
    PaymentStatus PaymentStatus,
    List<Guid> KotIds,
    List<Guid> StockMovementIds,
    Guid AuditEventId,
    List<Guid> OutboxEventIds,
    bool OfflineSaved,
    bool CloudSynced
);
