namespace Yashdeep.Domain.Orders;

public class LocalOrder
{
    private LocalOrder()
    {
        OrderNumber = string.Empty;
        Status = string.Empty;
    }

    public LocalOrder(Guid orderId, Guid tenantId, Guid branchId, string orderNumber, decimal totalAmount)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId is required.", nameof(orderId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(branchId));
        if (string.IsNullOrWhiteSpace(orderNumber)) throw new ArgumentException("OrderNumber is required.", nameof(orderNumber));

        OrderId = orderId;
        TenantId = tenantId;
        BranchId = branchId;
        OrderNumber = orderNumber;
        TotalAmount = totalAmount;
        Status = "Open";
        CreatedUtc = DateTime.UtcNow;
    }

    public Guid OrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string OrderNumber { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Status { get; private set; }
    public DateTime CreatedUtc { get; private set; }
}
