using Yashdeep.Domain.ValueObjects;
using Yashdeep.Shared.Time;

namespace Yashdeep.Domain.Entities.Inventory;

public class StockMovement
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public string ItemCode { get; private set; }
    public string ItemName { get; private set; }
    public StockMovementType MovementType { get; private set; }
    public decimal Quantity { get; private set; }
    public int? VolumeMlDeducted { get; private set; }
    public Guid? ReferenceTransactionId { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    private StockMovement()
    {
        ItemCode = string.Empty;
        ItemName = string.Empty;
    }

    public StockMovement(
        Guid id,
        Guid tenantId,
        Guid branchId,
        Guid inventoryItemId,
        string itemCode,
        string itemName,
        StockMovementType movementType,
        decimal quantity,
        IDateTimeProvider timeProvider,
        int? volumeMlDeducted = null,
        Guid? referenceTransactionId = null,
        bool allowNegativeMovement = false)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(branchId));
        if (inventoryItemId == Guid.Empty) throw new ArgumentException("InventoryItemId is required.", nameof(inventoryItemId));

        if (!allowNegativeMovement && quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero for stock movements.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        TenantId = tenantId;
        BranchId = branchId;
        InventoryItemId = inventoryItemId;
        ItemCode = itemCode ?? string.Empty;
        ItemName = itemName ?? throw new ArgumentNullException(nameof(itemName));
        MovementType = movementType;
        Quantity = quantity;
        VolumeMlDeducted = volumeMlDeducted;
        ReferenceTransactionId = referenceTransactionId;
        TimestampUtc = timeProvider.UtcNow;
    }
}
