using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yashdeep.Application.Interfaces;
using Yashdeep.Application.Services;
using Yashdeep.Infrastructure.Hardware;
using Yashdeep.Infrastructure.Persistence;
using Yashdeep.Infrastructure.Security;
using Yashdeep.Server.Api.Middleware;
using Yashdeep.Shared.Hardware;
using Yashdeep.Shared.Security;

var builder = WebApplication.CreateBuilder(args);

// Foundation Composition Root: Add framework services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register trusted device foundation services
builder.Services.AddSingleton<IDeviceRepository, InMemoryDeviceRepository>();
builder.Services.AddSingleton<IAuditEventLogger, InMemoryAuditEventLogger>();
builder.Services.AddSingleton<IDeviceTokenService, DeviceTokenService>();
builder.Services.AddSingleton<IDeviceIdentityProvider, TestDeviceIdentityProvider>();
builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddScoped<IDeviceRegistrationService, DeviceRegistrationService>();

var app = builder.Build();

app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

// Minimal health check endpoint for executable host validation
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "Yashdeep.Server.Api",
    TimestampUtc = DateTime.UtcNow,
    Version = "1.0.0"
}))
.WithName("HealthCheck");

app.MapControllers();

app.Run();

// Partial Program class declaration for WebApplicationFactory / Integration Testing
public partial class Program { }
