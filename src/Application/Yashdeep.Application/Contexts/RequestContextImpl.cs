using Yashdeep.Domain.Contexts;

namespace Yashdeep.Application.Contexts;

public class TenantContextImpl : ITenantContext
{
    public Guid TenantId { get; set; }
    public bool IsTenantResolved => TenantId != Guid.Empty;
}

public class BranchContextImpl : TenantContextImpl, IBranchContext
{
    public Guid? BranchId { get; set; }
    public bool IsBranchResolved => BranchId.HasValue && BranchId.Value != Guid.Empty;
    public bool IsOrganizationAccess { get; set; }
    public IReadOnlyCollection<Guid> AllowedBranchIds { get; set; } = Array.Empty<Guid>();
}

public class OutletContextImpl : BranchContextImpl, IOutletContext
{
    public Guid? OutletId { get; set; }
    public bool IsOutletResolved => OutletId.HasValue && OutletId.Value != Guid.Empty;
}

public class DeviceContextImpl : BranchContextImpl, IDeviceContext
{
    public Guid? TerminalId { get; set; }
    public Guid? DeviceId { get; set; }
    public bool IsDeviceResolved => DeviceId.HasValue && DeviceId.Value != Guid.Empty;
}

public class UserContextImpl : IUserContext
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public bool IsSystemAdmin { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
}

public class RequestContextImpl : IRequestContext
{
    public ITenantContext Tenant { get; set; } = new TenantContextImpl();
    public IBranchContext Branch { get; set; } = new BranchContextImpl();
    public IOutletContext Outlet { get; set; } = new OutletContextImpl();
    public IDeviceContext Device { get; set; } = new DeviceContextImpl();
    public IUserContext User { get; set; } = new UserContextImpl();
}
