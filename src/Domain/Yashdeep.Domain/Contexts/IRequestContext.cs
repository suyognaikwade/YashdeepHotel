namespace Yashdeep.Domain.Contexts;

public interface ITenantContext
{
    Guid TenantId { get; }
    bool IsTenantResolved { get; }
}

public interface IBranchContext : ITenantContext
{
    Guid? BranchId { get; }
    bool IsBranchResolved { get; }
    bool IsOrganizationAccess { get; }
    IReadOnlyCollection<Guid> AllowedBranchIds { get; }
}

public interface IOutletContext : IBranchContext
{
    Guid? OutletId { get; }
    bool IsOutletResolved { get; }
}

public interface IDeviceContext : IBranchContext
{
    Guid? TerminalId { get; }
    Guid? DeviceId { get; }
    bool IsDeviceResolved { get; }
}

public interface IUserContext
{
    Guid UserId { get; }
    string Username { get; }
    bool IsAuthenticated { get; }
    bool IsSystemAdmin { get; }
    IReadOnlyCollection<string> Roles { get; }
}

public interface IRequestContext
{
    ITenantContext Tenant { get; }
    IBranchContext Branch { get; }
    IOutletContext Outlet { get; }
    IDeviceContext Device { get; }
    IUserContext User { get; }
}
