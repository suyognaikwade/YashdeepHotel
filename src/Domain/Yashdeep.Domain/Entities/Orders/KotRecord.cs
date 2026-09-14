using Yashdeep.Domain.ValueObjects;

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
        IEnumerable<KotLineItem> items)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        OrderId = orderId;
        TenantId = tenantId;
        BranchId = branchId;
        KotNumber = kotNumber ?? throw new ArgumentNullException(nameof(kotNumber));
        TicketType = ticketType;
        TableNumber = tableNumber ?? string.Empty;
        WaiterName = waiterName ?? string.Empty;
        PrintedAtUtc = DateTime.UtcNow;
        _lineItems.AddRange(items ?? Array.Empty<KotLineItem>());
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
