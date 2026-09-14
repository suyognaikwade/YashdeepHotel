using Yashdeep.Domain.Enums;

namespace Yashdeep.Domain.Entities;

public class Tenant
{
    public Guid TenantId { get; private set; }
    public string LegalName { get; private set; } = string.Empty;
    public string TradeName { get; private set; } = string.Empty;
    public string GSTIN { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string ContactPhone { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; } = TenantStatus.Active;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    private readonly List<Organization> _organizations = new();
    public IReadOnlyCollection<Organization> Organizations => _organizations.AsReadOnly();

    private Tenant() { }

    public Tenant(Guid tenantId, string legalName, string tradeName, string gstin, string contactEmail, string contactPhone)
    {
        TenantId = tenantId == Guid.Empty ? Guid.NewGuid() : tenantId;
        LegalName = legalName ?? throw new ArgumentNullException(nameof(legalName));
        TradeName = tradeName ?? throw new ArgumentNullException(nameof(tradeName));
        GSTIN = gstin ?? string.Empty;
        ContactEmail = contactEmail ?? string.Empty;
        ContactPhone = contactPhone ?? string.Empty;
        Status = TenantStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateStatus(TenantStatus status)
    {
        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
