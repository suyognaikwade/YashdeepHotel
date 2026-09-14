using Yashdeep.Domain.ValueObjects;

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
        int? volumeMlDeducted = null,
        Guid? referenceTransactionId = null)
    {
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
        TimestampUtc = DateTime.UtcNow;
    }
}
