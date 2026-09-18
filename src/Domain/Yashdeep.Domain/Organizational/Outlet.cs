using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.Organizational.Events;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational;

public sealed class Outlet : AggregateRoot<OutletId>
{
    public TenantId TenantId { get; }
    public BranchId BranchId { get; }
    public string Name { get; private set; }
    public OutletType OutletType { get; private set; }
    public OutletStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Outlet(
        OutletId id,
        TenantId tenantId,
        BranchId branchId,
        string name,
        OutletType outletType,
        OutletStatus status,
        DateTime createdAtUtc)
        : base(id)
    {
        TenantId = tenantId ?? throw new InvalidOwnershipException("Outlet must belong to a valid tenant.");
        BranchId = branchId ?? throw new InvalidOwnershipException("Outlet must belong to a valid branch.");
        Name = name;
        OutletType = outletType;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public static Outlet Create(
        Branch branch,
        string name,
        OutletType outletType,
        OutletId? id = null,
        OutletStatus initialStatus = OutletStatus.Active)
    {
        if (branch is null)
        {
            throw new ArgumentNullException(nameof(branch));
        }

        return Create(
            branch.TenantId,
            branch.Id,
            name,
            outletType,
            id,
            initialStatus);
    }

    public static Outlet Create(
        TenantId tenantId,
        BranchId branchId,
        string name,
        OutletType outletType,
        OutletId? id = null,
        OutletStatus initialStatus = OutletStatus.Active)
    {
        if (tenantId is null)
        {
            throw new InvalidOwnershipException("TenantId is required to create an Outlet.");
        }

        if (branchId is null)
        {
            throw new InvalidOwnershipException("BranchId is required to create an Outlet.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Outlet name is required.");
        }

        var outletId = id ?? OutletId.New();
        var now = DateTime.UtcNow;

        var outlet = new Outlet(
            outletId,
            tenantId,
            branchId,
            name.Trim(),
            outletType,
            initialStatus,
            now);

        outlet.AddDomainEvent(new OutletCreatedEvent(
            outlet.Id,
            outlet.BranchId,
            outlet.TenantId,
            outlet.Name,
            outlet.OutletType,
            outlet.Status,
            now));

        return outlet;
    }

    public void ChangeStatus(OutletStatus newStatus)
    {
        if (Status == newStatus)
        {
            return;
        }

        var previous = Status;
        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new OutletStatusChangedEvent(
            Id,
            TenantId,
            previous,
            newStatus,
            UpdatedAtUtc));
    }

    public void Activate() => ChangeStatus(OutletStatus.Active);
    public void Deactivate() => ChangeStatus(OutletStatus.Inactive);
}
