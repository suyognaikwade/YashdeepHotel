namespace Yashdeep.Domain.Entities.Audit;

public class AuditEvent
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string EventType { get; private set; }
    public string Action { get; private set; }
    public Guid? UserId { get; private set; }
    public string PerformedBy { get; private set; }
    public Guid ReferenceId { get; private set; }
    public string DetailsJson { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    private AuditEvent()
    {
        EventType = string.Empty;
        Action = string.Empty;
        PerformedBy = string.Empty;
        DetailsJson = string.Empty;
    }

    public AuditEvent(
        Guid id,
        Guid tenantId,
        Guid branchId,
        string eventType,
        string action,
        Guid? userId,
        string performedBy,
        Guid referenceId,
        string detailsJson)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        TenantId = tenantId;
        BranchId = branchId;
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        Action = action ?? throw new ArgumentNullException(nameof(action));
        UserId = userId;
        PerformedBy = performedBy ?? string.Empty;
        ReferenceId = referenceId;
        DetailsJson = detailsJson ?? "{}";
        TimestampUtc = DateTime.UtcNow;
    }
}
