using Yashdeep.Domain.ValueObjects;
using Yashdeep.Shared.Time;

namespace Yashdeep.Domain.Entities.Orders;

public class KotRecord
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string KotNumber { get; private set; }
    public KotTicketType TicketType { get; private set; }
    public DateTime PrintedAtUtc { get; private set; }
    public string TableNumber { get; private set; }
    public string WaiterName { get; private set; }
    private readonly List<KotLineItem> _lineItems = new();
    public IReadOnlyCollection<KotLineItem> LineItems => _lineItems.AsReadOnly();

    private KotRecord()
    {
        KotNumber = string.Empty;
        TableNumber = string.Empty;
        WaiterName = string.Empty;
    }

    public KotRecord(
        Guid id,
        Guid orderId,
        Guid tenantId,
        Guid branchId,
        string kotNumber,
        KotTicketType ticketType,
        string tableNumber,
        string waiterName,
        IEnumerable<KotLineItem> items,
        IDateTimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(branchId));
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId is required.", nameof(orderId));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        OrderId = orderId;
        TenantId = tenantId;
        BranchId = branchId;
        KotNumber = string.IsNullOrWhiteSpace(kotNumber) ? throw new ArgumentException("KotNumber cannot be empty.", nameof(kotNumber)) : kotNumber;
        TicketType = ticketType;
        TableNumber = tableNumber ?? string.Empty;
        WaiterName = waiterName ?? string.Empty;
        PrintedAtUtc = timeProvider.UtcNow;

        var itemList = items?.ToList() ?? new List<KotLineItem>();
        if (itemList.Count == 0)
            throw new ArgumentException("KOT line items cannot be empty.", nameof(items));

        _lineItems.AddRange(itemList);
    }
}

public record KotLineItem(
    Guid MenuItemId,
    string ItemCode,
    string EnglishName,
    string MarathiName,
    decimal Quantity,
    int? UnitVolumeMl
);
