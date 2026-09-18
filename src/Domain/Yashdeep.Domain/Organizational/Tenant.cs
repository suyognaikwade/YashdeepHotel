using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.Organizational.Events;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational;

public sealed class Tenant : AggregateRoot<TenantId>
{
    public string LegalName { get; private set; }
    public string TradeName { get; private set; }
    public string ContactEmail { get; private set; }
    public string ContactPhone { get; private set; }
    public TenantStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Tenant(
        TenantId id,
        string legalName,
        string tradeName,
        string contactEmail,
        string contactPhone,
        TenantStatus status,
        DateTime createdAtUtc)
        : base(id)
    {
        LegalName = legalName;
        TradeName = tradeName;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public static Tenant Create(
        string legalName,
        string tradeName,
        string contactEmail,
        string contactPhone,
        TenantId? id = null,
        TenantStatus initialStatus = TenantStatus.Pending)
    {
        if (string.IsNullOrWhiteSpace(legalName))
        {
            throw new DomainException("Tenant legal name is required.");
        }

        if (string.IsNullOrWhiteSpace(tradeName))
        {
            throw new DomainException("Tenant trade name is required.");
        }

        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            throw new DomainException("Tenant contact email is required.");
        }

        if (string.IsNullOrWhiteSpace(contactPhone))
        {
            throw new DomainException("Tenant contact phone is required.");
        }

        var tenantId = id ?? TenantId.New();
        var now = DateTime.UtcNow;

        var tenant = new Tenant(
            tenantId,
            legalName.Trim(),
            tradeName.Trim(),
            contactEmail.Trim(),
            contactPhone.Trim(),
            initialStatus,
            now);

        tenant.AddDomainEvent(new TenantCreatedEvent(
            tenant.Id,
            tenant.LegalName,
            tenant.TradeName,
            tenant.Status,
            now));

        return tenant;
    }

    public void ChangeStatus(TenantStatus newStatus)
    {
        if (Status == TenantStatus.Terminated)
        {
            throw new InvalidStateTransitionException("Cannot change status of a terminated tenant.");
        }

        if (Status == newStatus)
        {
            return;
        }

        var previousStatus = Status;
        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new TenantStatusChangedEvent(
            Id,
            previousStatus,
            newStatus,
            UpdatedAtUtc));
    }

    public void Activate() => ChangeStatus(TenantStatus.Active);

    public void Suspend() => ChangeStatus(TenantStatus.Suspended);

    public void Terminate() => ChangeStatus(TenantStatus.Terminated);
}
