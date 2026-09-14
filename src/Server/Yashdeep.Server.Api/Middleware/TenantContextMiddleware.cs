using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Yashdeep.Shared.Security;

namespace Yashdeep.Server.Api.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    public const string TenantHeaderName = "X-Tenant-Id";
    public const string BranchHeaderName = "X-Branch-Id";

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var deviceTokenService = context.RequestServices.GetService<IDeviceTokenService>();

        string? authHeader = context.Request.Headers["Authorization"];
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            string token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader.Substring(7).Trim()
                : authHeader.StartsWith("DeviceToken ", StringComparison.OrdinalIgnoreCase)
                    ? authHeader.Substring(12).Trim()
                    : authHeader.Trim();

            if (deviceTokenService != null)
            {
                var validationResult = deviceTokenService.ValidateToken(token);
                if (validationResult.IsValid && validationResult.Payload != null)
                {
                    context.Items["IsAuthenticated"] = true;
                    context.Items["TenantId"] = validationResult.Payload.TenantId;
                    context.Items["BranchId"] = validationResult.Payload.BranchId;
                    context.Items["DeviceTokenPayload"] = validationResult.Payload;

                    // Header manipulation tamper detection
                    if (context.Request.Headers.TryGetValue(TenantHeaderName, out var headerTenantStr) &&
                        Guid.TryParse(headerTenantStr, out var headerTenantId) &&
                        headerTenantId != Guid.Empty &&
                        headerTenantId != validationResult.Payload.TenantId)
                    {
                        context.Items["TenantMismatch"] = true;
                    }

                    if (context.Request.Headers.TryGetValue(BranchHeaderName, out var headerBranchStr) &&
                        Guid.TryParse(headerBranchStr, out var headerBranchId) &&
                        headerBranchId != Guid.Empty &&
                        headerBranchId != validationResult.Payload.BranchId)
                    {
                        context.Items["BranchMismatch"] = true;
                    }

                    await _next(context);
                    return;
                }
                else
                {
                    context.Items["IsAuthenticated"] = false;
                    context.Items["TokenValidationError"] = validationResult.ErrorReason;
                }
            }
        }

        // Unauthenticated request fallback from headers
        context.Items["IsAuthenticated"] = false;

        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var unauthTenantStr) &&
            Guid.TryParse(unauthTenantStr, out var unauthTenantId))
        {
            context.Items["TenantId"] = unauthTenantId;
        }

        if (context.Request.Headers.TryGetValue(BranchHeaderName, out var unauthBranchStr) &&
            Guid.TryParse(unauthBranchStr, out var unauthBranchId))
        {
            context.Items["BranchId"] = unauthBranchId;
        }

        await _next(context);
    }
}
