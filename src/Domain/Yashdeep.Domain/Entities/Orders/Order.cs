using Yashdeep.Domain.ValueObjects;
using Yashdeep.Shared.Time;

namespace Yashdeep.Domain.Entities.Orders;

public class Order
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid LocationId { get; private set; }
    public string TableNumber { get; private set; }
    public SectionTier Section { get; private set; }
    public OrderType OrderType { get; private set; }
    public OrderStatus Status { get; private set; }
    public string OrderNumber { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public Guid CaptainUserId { get; private set; }
    public string WaiterName { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private readonly List<KotRecord> _kots = new();
    public IReadOnlyCollection<KotRecord> Kots => _kots.AsReadOnly();

    private Order()
    {
        TableNumber = string.Empty;
        OrderNumber = string.Empty;
        WaiterName = string.Empty;
    }

    public Order(
        Guid id,
        Guid tenantId,
        Guid branchId,
        Guid locationId,
        string tableNumber,
        SectionTier section,
        OrderType orderType,
        string orderNumber,
        DateOnly businessDate,
        Guid captainUserId,
        string waiterName,
        IDateTimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(branchId));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        TenantId = tenantId;
        BranchId = branchId;
        LocationId = locationId;
        TableNumber = tableNumber ?? throw new ArgumentNullException(nameof(tableNumber));
        Section = section;
        OrderType = orderType;
        Status = OrderStatus.Open;
        OrderNumber = string.IsNullOrWhiteSpace(orderNumber) ? throw new ArgumentException("Order number cannot be empty.", nameof(orderNumber)) : orderNumber;
        BusinessDate = businessDate;
        CaptainUserId = captainUserId;
        WaiterName = waiterName ?? string.Empty;
        CreatedAtUtc = timeProvider.UtcNow;
    }

    public OrderItem AddItem(
        Guid menuItemId,
        string itemCode,
        string englishName,
        string marathiName,
        DepartmentType department,
        decimal quantity,
        Money unitPrice,
        int? unitVolumeMl = null)
    {
        if (Status != OrderStatus.Open)
            throw new InvalidOperationException($"Cannot add items to order in status {Status}.");

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        var existingItem = _items.FirstOrDefault(i => i.MenuItemId == menuItemId && i.UnitPrice == unitPrice && !i.IsSentToKot);
        if (existingItem != null)
        {
            existingItem.UpdateQuantity(existingItem.Quantity + quantity);
            return existingItem;
        }

        var item = new OrderItem(Guid.NewGuid(), Id, menuItemId, itemCode, englishName, marathiName, department, quantity, unitPrice, unitVolumeMl);
        _items.Add(item);
        return item;
    }

    public KotRecord GenerateKot(KotTicketType ticketType, string kotNumber, IDateTimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != OrderStatus.Open)
            throw new InvalidOperationException($"Cannot generate KOT for order in status {Status}.");

        DepartmentType targetDept = ticketType switch
        {
            KotTicketType.KotKitchen => DepartmentType.Kitchen,
            KotTicketType.BotBar => DepartmentType.Bar,
            _ => throw new ArgumentOutOfRangeException(nameof(ticketType), "Unsupported ticket type.")
        };

        var pendingItems = _items.Where(i => !i.IsSentToKot && i.Department == targetDept).ToList();

        if (pendingItems.Count == 0)
            throw new InvalidOperationException($"No unsent items found for department {targetDept}.");

        var lineItems = pendingItems.Select(i => new KotLineItem(
            i.MenuItemId,
            i.ItemCode,
            i.EnglishName,
            i.MarathiName,
            i.Quantity,
            i.UnitVolumeMl
        )).ToList();

        var kot = new KotRecord(
            Guid.NewGuid(),
            Id,
            TenantId,
            BranchId,
            kotNumber,
            ticketType,
            TableNumber,
            WaiterName,
            lineItems,
            timeProvider
        );

        foreach (var item in pendingItems)
        {
            item.MarkSentToKot();
        }

        _kots.Add(kot);
        return kot;
    }

    public Money CalculateSubTotal()
    {
        return _items.Aggregate(Money.Zero, (acc, item) => acc + item.SubTotal);
    }

    public Money CalculateFoodSubTotal()
    {
        return _items.Where(i => i.Department == DepartmentType.Kitchen || i.Department == DepartmentType.Beverage)
                     .Aggregate(Money.Zero, (acc, item) => acc + item.SubTotal);
    }

    public Money CalculateLiquorSubTotal()
    {
        return _items.Where(i => i.Department == DepartmentType.Bar)
                     .Aggregate(Money.Zero, (acc, item) => acc + item.SubTotal);
    }

    public void MarkBilled()
    {
        if (Status != OrderStatus.Open)
            throw new InvalidOperationException($"Cannot mark order billed when status is {Status}. Order is already billed or closed.");
        Status = OrderStatus.Billed;
    }

    public void MarkCompleted(IDateTimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status != OrderStatus.Billed && Status != OrderStatus.Open)
            throw new InvalidOperationException($"Cannot complete order in status {Status}.");
        Status = OrderStatus.Completed;
        ClosedAtUtc = timeProvider.UtcNow;
    }
}
