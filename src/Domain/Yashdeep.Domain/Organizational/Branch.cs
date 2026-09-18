using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.Organizational.Events;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational;

public sealed class Branch : AggregateRoot<BranchId>
{
    public TenantId TenantId { get; }
    public OrganizationId OrganizationId { get; }
    public string Name { get; private set; }
    public string BranchCode { get; private set; }
    public Address Address { get; private set; }
    public BranchStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Branch(
        BranchId id,
        TenantId tenantId,
        OrganizationId organizationId,
        string name,
        string branchCode,
        Address address,
        BranchStatus status,
        DateTime createdAtUtc)
        : base(id)
    {
        TenantId = tenantId ?? throw new InvalidOwnershipException("Branch must belong to a valid tenant.");
        OrganizationId = organizationId ?? throw new InvalidOwnershipException("Branch must belong to a valid organization.");
        Name = name;
        BranchCode = branchCode;
        Address = address;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public static Branch Create(
        Organization organization,
        string name,
        string branchCode,
        Address address,
        BranchId? id = null,
        BranchStatus initialStatus = BranchStatus.Active)
    {
        if (organization is null)
        {
            throw new ArgumentNullException(nameof(organization));
        }

        return Create(
            organization.TenantId,
            organization.Id,
            name,
            branchCode,
            address,
            id,
            initialStatus);
    }

    public static Branch Create(
        TenantId tenantId,
        OrganizationId organizationId,
        string name,
        string branchCode,
        Address address,
        BranchId? id = null,
        BranchStatus initialStatus = BranchStatus.Active)
    {
        if (tenantId is null)
        {
            throw new InvalidOwnershipException("TenantId is required to create a Branch.");
        }

        if (organizationId is null)
        {
            throw new InvalidOwnershipException("OrganizationId is required to create a Branch.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Branch name is required.");
        }

        if (string.IsNullOrWhiteSpace(branchCode))
        {
            throw new DomainException("Branch code is required.");
        }

        if (address is null)
        {
            throw new DomainException("Branch address is required.");
        }

        var branchId = id ?? BranchId.New();
        var now = DateTime.UtcNow;

        var branch = new Branch(
            branchId,
            tenantId,
            organizationId,
            name.Trim(),
            branchCode.Trim().ToUpperInvariant(),
            address,
            initialStatus,
            now);

        branch.AddDomainEvent(new BranchCreatedEvent(
            branch.Id,
            branch.OrganizationId,
            branch.TenantId,
            branch.Name,
            branch.BranchCode,
            branch.Status,
            now));

        return branch;
    }

    public void ChangeStatus(BranchStatus newStatus)
    {
        if (Status == BranchStatus.Closed)
        {
            throw new InvalidStateTransitionException("Cannot change status of a closed branch.");
        }

        if (Status == newStatus)
        {
            return;
        }

        var previous = Status;
        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new BranchStatusChangedEvent(
            Id,
            TenantId,
            previous,
            newStatus,
            UpdatedAtUtc));
    }

    public void Activate() => ChangeStatus(BranchStatus.Active);
    public void Deactivate() => ChangeStatus(BranchStatus.Inactive);
    public void Close() => ChangeStatus(BranchStatus.Closed);
}
