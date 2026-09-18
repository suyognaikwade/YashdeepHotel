namespace Yashdeep.Domain.ValueObjects;

public enum BillStatus
{
    Draft = 1,
    Printed = 2,
    Paid = 3,
    Cancelled = 4
}

public enum PaymentMethod
{
    Cash = 1,
    UpiQr = 2,
    Card = 3,
    CustomerLedger = 4
}

public enum PaymentStatus
{
    Unpaid = 1,
    PartiallyPaid = 2,
    Paid = 3
}

public enum StockMovementType
{
    SaleDeduction = 1,
    InwardPurchase = 2,
    InterStockTransfer = 3,
    Adjustment = 4
}

public enum OutboxMessageStatus
{
    Pending = 1,
    Uploaded = 2,
    Failed = 3
}
