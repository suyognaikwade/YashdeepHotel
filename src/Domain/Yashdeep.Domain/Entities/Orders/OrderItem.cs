using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Entities.Orders;

public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid MenuItemId { get; private set; }
    public string ItemCode { get; private set; }
    public string EnglishName { get; private set; }
    public string MarathiName { get; private set; }
    public DepartmentType Department { get; private set; }
    public decimal Quantity { get; private set; }
    public int? UnitVolumeMl { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money SubTotal { get; private set; }
    public bool IsSentToKot { get; private set; }

    private OrderItem()
    {
        ItemCode = string.Empty;
        EnglishName = string.Empty;
        MarathiName = string.Empty;
    }

    public OrderItem(
        Guid id,
        Guid orderId,
        Guid menuItemId,
        string itemCode,
        string englishName,
        string marathiName,
        DepartmentType department,
        decimal quantity,
        Money unitPrice,
        int? unitVolumeMl = null)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        OrderId = orderId;
        MenuItemId = menuItemId;
        ItemCode = itemCode ?? string.Empty;
        EnglishName = englishName ?? throw new ArgumentNullException(nameof(englishName));
        MarathiName = marathiName ?? englishName;
        Department = department;
        Quantity = quantity;
        UnitPrice = unitPrice;
        UnitVolumeMl = unitVolumeMl;
        SubTotal = (unitPrice * quantity).Round();
        IsSentToKot = false;
    }

    public void UpdateQuantity(decimal newQuantity)
    {
        if (newQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(newQuantity), "Quantity must be greater than zero.");
        Quantity = newQuantity;
        SubTotal = (UnitPrice * Quantity).Round();
    }

    public void MarkSentToKot()
    {
        IsSentToKot = true;
    }
}
