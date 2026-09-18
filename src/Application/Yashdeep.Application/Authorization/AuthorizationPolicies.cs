using Microsoft.AspNetCore.Authorization;
using Yashdeep.Domain.Contexts;

namespace Yashdeep.Application.Authorization;

public class TenantRequirement : IAuthorizationRequirement { }

public class OrganizationAccessRequirement : IAuthorizationRequirement { }

public class BranchAccessRequirement : IAuthorizationRequirement { }

public class OutletAccessRequirement : IAuthorizationRequirement { }

public class DeviceAccessRequirement : IAuthorizationRequirement { }

public class SystemAdminRequirement : IAuthorizationRequirement { }

public class TenantAuthorizationHandler : AuthorizationHandler<TenantRequirement>
{
    private readonly IRequestContext _requestContext;

    public TenantAuthorizationHandler(IRequestContext requestContext)
    {
        _requestContext = requestContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantRequirement requirement)
    {
        if (_requestContext.Tenant.IsTenantResolved)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public class OrganizationAccessAuthorizationHandler : AuthorizationHandler<OrganizationAccessRequirement>
{
    private readonly IRequestContext _requestContext;

    public OrganizationAccessAuthorizationHandler(IRequestContext requestContext)
    {
        _requestContext = requestContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizationAccessRequirement requirement)
    {
        if (_requestContext.Tenant.IsTenantResolved && (_requestContext.Branch.IsOrganizationAccess || _requestContext.User.IsSystemAdmin))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public class BranchAccessAuthorizationHandler : AuthorizationHandler<BranchAccessRequirement>
{
    private readonly IRequestContext _requestContext;

    public BranchAccessAuthorizationHandler(IRequestContext requestContext)
    {
        _requestContext = requestContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, BranchAccessRequirement requirement)
    {
        if (_requestContext.Tenant.IsTenantResolved)
        {
            if (_requestContext.User.IsSystemAdmin || _requestContext.Branch.IsOrganizationAccess)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            if (_requestContext.Branch.IsBranchResolved && _requestContext.Branch.AllowedBranchIds.Contains(_requestContext.Branch.BranchId!.Value))
            {
                context.Succeed(requirement);
            }
        }
        return Task.CompletedTask;
    }
}

public class OutletAccessAuthorizationHandler : AuthorizationHandler<OutletAccessRequirement>
{
    private readonly IRequestContext _requestContext;

    public OutletAccessAuthorizationHandler(IRequestContext requestContext)
    {
        _requestContext = requestContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OutletAccessRequirement requirement)
    {
        if (_requestContext.Tenant.IsTenantResolved && _requestContext.Outlet.IsOutletResolved)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public class DeviceAccessAuthorizationHandler : AuthorizationHandler<DeviceAccessRequirement>
{
    private readonly IRequestContext _requestContext;

    public DeviceAccessAuthorizationHandler(IRequestContext requestContext)
    {
        _requestContext = requestContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, DeviceAccessRequirement requirement)
    {
        if (_requestContext.Tenant.IsTenantResolved && _requestContext.Device.IsDeviceResolved)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public class SystemAdminAuthorizationHandler : AuthorizationHandler<SystemAdminRequirement>
{
    private readonly IRequestContext _requestContext;

    public SystemAdminAuthorizationHandler(IRequestContext requestContext)
    {
        _requestContext = requestContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SystemAdminRequirement requirement)
    {
        if (_requestContext.User.IsSystemAdmin)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
