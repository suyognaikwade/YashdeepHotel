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

builder.Services.AddControllers();

// Register trusted device foundation services
builder.Services.AddSingleton<IDeviceRepository, InMemoryDeviceRepository>();
builder.Services.AddSingleton<IAuditEventLogger, InMemoryAuditEventLogger>();
builder.Services.AddSingleton<IDeviceTokenService, DeviceTokenService>();
builder.Services.AddSingleton<IDeviceIdentityProvider, TestDeviceIdentityProvider>();
builder.Services.AddScoped<IDeviceRegistrationService, DeviceRegistrationService>();

var app = builder.Build();

app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
