namespace Yashdeep.Domain.Entities;

public class Organization
{
    public Guid OrganizationId { get; private set; }
    public Guid TenantId { get; private set; }
    public string LegalName { get; private set; } = string.Empty;
    public string ExciseLicenseNumber { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private Organization() { }

    public Organization(Guid organizationId, Guid tenantId, string legalName, string exciseLicenseNumber = "")
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));

        OrganizationId = organizationId == Guid.Empty ? Guid.NewGuid() : organizationId;
        TenantId = tenantId;
        LegalName = legalName ?? throw new ArgumentNullException(nameof(legalName));
        ExciseLicenseNumber = exciseLicenseNumber ?? string.Empty;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
