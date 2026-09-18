using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.Organizational.Events;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational;

public sealed class Organization : AggregateRoot<OrganizationId>
{
    public TenantId TenantId { get; }
    public string LegalName { get; private set; }
    public string TradeName { get; private set; }
    public TaxNumber TaxNumber { get; private set; }
    public LicenseNumber LicenseNumber { get; private set; }
    public OrganizationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Organization(
        OrganizationId id,
        TenantId tenantId,
        string legalName,
        string tradeName,
        TaxNumber taxNumber,
        LicenseNumber licenseNumber,
        OrganizationStatus status,
        DateTime createdAtUtc)
        : base(id)
    {
        TenantId = tenantId ?? throw new InvalidOwnershipException("Organization must belong to a valid tenant.");
        LegalName = legalName;
        TradeName = tradeName;
        TaxNumber = taxNumber;
        LicenseNumber = licenseNumber;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public static Organization Create(
        TenantId tenantId,
        string legalName,
        string tradeName,
        TaxNumber taxNumber,
        LicenseNumber licenseNumber,
        OrganizationId? id = null,
        OrganizationStatus initialStatus = OrganizationStatus.Active)
    {
        if (tenantId is null)
        {
            throw new InvalidOwnershipException("TenantId is required to create an Organization.");
        }

        if (string.IsNullOrWhiteSpace(legalName))
        {
            throw new DomainException("Organization legal name is required.");
        }

        if (string.IsNullOrWhiteSpace(tradeName))
        {
            throw new DomainException("Organization trade name is required.");
        }

        if (taxNumber is null)
        {
            throw new DomainException("Tax number (GSTIN) is required.");
        }

        if (licenseNumber is null)
        {
            throw new DomainException("License number is required.");
        }

        var orgId = id ?? OrganizationId.New();
        var now = DateTime.UtcNow;

        var org = new Organization(
            orgId,
            tenantId,
            legalName.Trim(),
            tradeName.Trim(),
            taxNumber,
            licenseNumber,
            initialStatus,
            now);

        org.AddDomainEvent(new OrganizationCreatedEvent(
            org.Id,
            org.TenantId,
            org.LegalName,
            org.Status,
            now));

        return org;
    }

    public void ChangeStatus(OrganizationStatus newStatus)
    {
        if (Status == OrganizationStatus.Archived)
        {
            throw new InvalidStateTransitionException("Cannot change status of an archived organization.");
        }

        if (Status == newStatus)
        {
            return;
        }

        var previous = Status;
        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new OrganizationStatusChangedEvent(
            Id,
            TenantId,
            previous,
            newStatus,
            UpdatedAtUtc));
    }

    public void Activate() => ChangeStatus(OrganizationStatus.Active);
    public void Deactivate() => ChangeStatus(OrganizationStatus.Inactive);
    public void Archive() => ChangeStatus(OrganizationStatus.Archived);
}
