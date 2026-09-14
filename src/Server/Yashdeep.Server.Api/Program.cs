using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Authorization;
using Yashdeep.Application.Contexts;
using Yashdeep.Application.Services;
using Yashdeep.Domain.Contexts;
using Yashdeep.Persistence.Cloud;
using Yashdeep.Persistence.Cloud.Interceptors;
using Yashdeep.Server.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register Contexts (Scoped for per-request isolation)
builder.Services.AddScoped<RequestContextImpl>();
builder.Services.AddScoped<IRequestContext>(sp => sp.GetRequiredService<RequestContextImpl>());
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<RequestContextImpl>().Tenant);
builder.Services.AddScoped<IBranchContext>(sp => sp.GetRequiredService<RequestContextImpl>().Branch);
builder.Services.AddScoped<IOutletContext>(sp => sp.GetRequiredService<RequestContextImpl>().Outlet);
builder.Services.AddScoped<IDeviceContext>(sp => sp.GetRequiredService<RequestContextImpl>().Device);
builder.Services.AddScoped<IUserContext>(sp => sp.GetRequiredService<RequestContextImpl>().User);

// Register Tenant Validation Service (Singleton for memory test seeding / replaceable in production)
builder.Services.AddSingleton<ITenantValidationService, InMemoryTenantValidationService>();

// Register Interceptor & DbContext
builder.Services.AddScoped<CloudTenantInterceptor>();
builder.Services.AddDbContext<CloudDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<CloudTenantInterceptor>();
    options.UseInMemoryDatabase("YashdeepCloudDb")
           .AddInterceptors(interceptor);
});

// Register Authorization Handlers
builder.Services.AddScoped<IAuthorizationHandler, TenantAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OrganizationAccessAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, BranchAccessAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OutletAccessAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, DeviceAccessAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, SystemAdminAuthorizationHandler>();

// Configure Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantOnly", policy => policy.Requirements.Add(new TenantRequirement()));
    options.AddPolicy("OrganizationScoped", policy => policy.Requirements.Add(new OrganizationAccessRequirement()));
    options.AddPolicy("BranchScoped", policy => policy.Requirements.Add(new BranchAccessRequirement()));
    options.AddPolicy("OutletScoped", policy => policy.Requirements.Add(new OutletAccessRequirement()));
    options.AddPolicy("DeviceScoped", policy => policy.Requirements.Add(new DeviceAccessRequirement()));
    options.AddPolicy("SystemAdminOnly", policy => policy.Requirements.Add(new SystemAdminRequirement()));
});

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<RequestContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
