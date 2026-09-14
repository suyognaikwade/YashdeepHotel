using System.Security.Claims;
using Yashdeep.Application.Interfaces;

namespace Yashdeep.Server.Api.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, ITenantContext tenantContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = httpContext.User.FindFirst("tenant_id")?.Value;
            var orgClaim = httpContext.User.FindFirst("org_id")?.Value;
            var userClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            Guid.TryParse(tenantClaim, out var tenantId);
            Guid.TryParse(orgClaim, out var orgId);
            Guid.TryParse(userClaim, out var userId);

            if (tenantId != Guid.Empty && userId != Guid.Empty)
            {
                tenantContext.SetContext(tenantId, orgId, userId);
            }
        }
        else
        {
            if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var headerTenant) &&
                Guid.TryParse(headerTenant, out var tenantId))
            {
                tenantContext.SetContext(tenantId, Guid.Empty, Guid.Empty);
            }
        }

        await _next(httpContext);
    }
}
