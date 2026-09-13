using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Yashdeep.Server.Api.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    public const string TenantHeaderName = "X-Tenant-Id";

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var tenantIdStr) &&
            Guid.TryParse(tenantIdStr, out var tenantId))
        {
            context.Items["TenantId"] = tenantId;
        }

        await _next(context);
    }
}
