using System.Security.Claims;
using Yashdeep.Application.Contexts;
using Yashdeep.Application.Services;
using Yashdeep.Domain.Contexts;
using Yashdeep.Domain.Exceptions;

namespace Yashdeep.Server.Api.Middleware;

public class RequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRequestContext requestContext, ITenantValidationService tenantValidationService)
    {
        var tenantContext = (TenantContextImpl)requestContext.Tenant;
        var branchContext = (BranchContextImpl)requestContext.Branch;
        var outletContext = (OutletContextImpl)requestContext.Outlet;
        var deviceContext = (DeviceContextImpl)requestContext.Device;
        var userContext = (UserContextImpl)requestContext.User;

        // 1. Resolve User Context from Authenticated ClaimsPrincipal
        if (context.User.Identity?.IsAuthenticated == true)
        {
            userContext.IsAuthenticated = true;

            var userIdClaim = context.User.FindFirst("user_id")?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                userContext.UserId = userId;
            }

            userContext.Username = context.User.FindFirst(ClaimTypes.Name)?.Value ?? context.User.Identity.Name ?? string.Empty;

            var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            userContext.Roles = roles;
            userContext.IsSystemAdmin = roles.Contains("SystemAdmin") || context.User.HasClaim("is_system_admin", "true");

            // 2. Resolve Tenant ID from Claims (or Header/Subdomain as fallbacks)
            Guid? tenantId = null;
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            if (Guid.TryParse(tenantClaim, out var parsedTenantClaim))
            {
                tenantId = parsedTenantClaim;
            }
            else if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) && Guid.TryParse(tenantHeader.ToString(), out var parsedHeaderTenant))
            {
                tenantId = parsedHeaderTenant;
            }

            if (tenantId.HasValue)
            {
                var tenant = await tenantValidationService.GetTenantByIdAsync(tenantId.Value, context.RequestAborted);
                if (tenant == null || !tenant.IsActive)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { error = "Invalid or inactive tenant." });
                    return;
                }

                tenantContext.TenantId = tenantId.Value;

                // Load User Details from Server Authority
                var dbUser = await tenantValidationService.GetUserByIdAsync(tenantId.Value, userContext.UserId, context.RequestAborted);
                if (dbUser != null)
                {
                    userContext.IsSystemAdmin |= dbUser.IsSystemAdmin;
                    branchContext.IsOrganizationAccess = dbUser.HasOrganizationWideAccess || dbUser.IsSystemAdmin;
                }

                var allowedBranchIds = await tenantValidationService.GetAuthorizedBranchIdsForUserAsync(tenantId.Value, userContext.UserId, context.RequestAborted);
                branchContext.AllowedBranchIds = allowedBranchIds;
            }

            // 3. Resolve Branch ID from Server-Controlled Claims / Headers
            Guid? branchId = null;
            var branchClaim = context.User.FindFirst("branch_id")?.Value;
            if (Guid.TryParse(branchClaim, out var parsedBranchClaim))
            {
                branchId = parsedBranchClaim;
            }
            else if (context.Request.Headers.TryGetValue("X-Branch-Id", out var branchHeader) && Guid.TryParse(branchHeader.ToString(), out var parsedHeaderBranch))
            {
                branchId = parsedHeaderBranch;
            }

            if (branchId.HasValue && tenantContext.IsTenantResolved)
            {
                var isAuthorized = await tenantValidationService.IsBranchAuthorizedForUserAsync(tenantContext.TenantId, userContext.UserId, branchId.Value, context.RequestAborted);
                if (!isAuthorized)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = $"User is not authorized to access branch {branchId.Value}." });
                    return;
                }

                branchContext.BranchId = branchId.Value;
            }

            // 4. Resolve Outlet ID
            if (context.Request.Headers.TryGetValue("X-Outlet-Id", out var outletHeader) && Guid.TryParse(outletHeader.ToString(), out var parsedOutlet))
            {
                outletContext.OutletId = parsedOutlet;
            }

            // 5. Resolve Device Context
            if (context.Request.Headers.TryGetValue("X-Device-Id", out var deviceHeader) && Guid.TryParse(deviceHeader.ToString(), out var parsedDevice))
            {
                deviceContext.DeviceId = parsedDevice;
            }
            if (context.Request.Headers.TryGetValue("X-Terminal-Id", out var terminalHeader) && Guid.TryParse(terminalHeader.ToString(), out var parsedTerminal))
            {
                deviceContext.TerminalId = parsedTerminal;
            }

            // 6. Validate Client-Supplied Identifiers (Route / Query / Body) against Server Context
            var routeTenantStr = context.Request.RouteValues["tenantId"]?.ToString();
            if (Guid.TryParse(routeTenantStr, out var routeTenantId) && routeTenantId != tenantContext.TenantId)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Manipulated tenant identifier in route rejected." });
                return;
            }

            var queryTenantStr = context.Request.Query["tenantId"].ToString();
            if (Guid.TryParse(queryTenantStr, out var queryTenantId) && queryTenantId != tenantContext.TenantId)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Manipulated tenant identifier in query parameter rejected." });
                return;
            }

            var routeBranchStr = context.Request.RouteValues["branchId"]?.ToString();
            if (Guid.TryParse(routeBranchStr, out var routeBranchId))
            {
                if (branchContext.IsBranchResolved && routeBranchId != branchContext.BranchId && !branchContext.IsOrganizationAccess && !userContext.IsSystemAdmin)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "Manipulated branch identifier in route rejected." });
                    return;
                }
            }
        }

        await _next(context);
    }
}
