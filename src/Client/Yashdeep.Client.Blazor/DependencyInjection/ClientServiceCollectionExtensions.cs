using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Application.Connectivity;
using Yashdeep.Application.Entitlements;
using Yashdeep.Application.Interfaces;
using Yashdeep.Application.Outbox;
using Yashdeep.Application.Persistence;
using Yashdeep.Application.Pos.UI;
using Yashdeep.Application.Pos.Workflows;
using Yashdeep.Application.Services;
using Yashdeep.Domain.Connectivity;
using Yashdeep.Infrastructure.Connectivity;
using Yashdeep.Infrastructure.Hardware;
using Yashdeep.Infrastructure.Persistence;
using Yashdeep.Infrastructure.Printing;
using Yashdeep.Infrastructure.Security;
using Yashdeep.Infrastructure.SyncEngine;
using Yashdeep.Persistence.Local;
using Yashdeep.Persistence.Local.Repositories;
using Yashdeep.Persistence.Local.Security;
using Yashdeep.Shared.Hardware;
using Yashdeep.Shared.Security;
using Yashdeep.Shared.Time;

namespace Yashdeep.Client.Blazor.DependencyInjection;

public static class ClientServiceCollectionExtensions
{
    public static IServiceCollection AddYashdeepClientServices(this IServiceCollection services, Action<ClientServicesOptions>? configure = null)
    {
        var options = new ClientServicesOptions();
        configure?.Invoke(options);

        // Time & System Providers
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IMonotonicClock, SystemMonotonicClock>();
        services.AddSingleton<IClockTamperDetector, ClockTamperDetector>();

        // Tenant Context & Capability Evaluator
        services.AddScoped<Application.Common.Interfaces.ITenantContext, Application.Common.Interfaces.TenantContext>();
        services.AddSingleton<IEntitlementCache, EntitlementCache>();
        services.AddSingleton<Ed25519EntitlementTokenService>();
        services.AddSingleton<IEntitlementTokenValidator>(sp => sp.GetRequiredService<Ed25519EntitlementTokenService>());
        services.AddSingleton<IEntitlementTokenVerifier>(sp => sp.GetRequiredService<Ed25519EntitlementTokenService>());
        services.AddSingleton<ICapabilityEvaluator, CapabilityEvaluator>();
        services.AddScoped<IUiVisibilityEvaluator, CapabilityViewHelper>();

        // Device Identity & Registration
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IDeviceIdentityProvider, WindowsDeviceIdentityProvider>();
        }
        else if (OperatingSystem.IsAndroid())
        {
            services.AddSingleton<IDeviceIdentityProvider, AndroidDeviceIdentityProvider>();
        }
        else
        {
            services.AddSingleton<IDeviceIdentityProvider, TestDeviceIdentityProvider>();
        }

        services.AddSingleton<IDeviceTokenService, DeviceTokenService>();
        services.AddScoped<IDeviceRepository, InMemoryDeviceRepository>();
        services.AddScoped<IDeviceRegistrationService, DeviceRegistrationService>();

        // Local SQLite Persistence Setup
        services.AddSingleton<Application.Common.Interfaces.ISQLiteKeyProvider, EnvironmentVariableKeyProvider>();
        services.AddDbContext<LocalPosDbContext>(opts =>
            opts.UseSqlite(options.LocalConnectionString ?? "Data Source=yashdeep_local_pos.db"));

        services.AddSingleton<LocalPosMemoryDbContext>();
        services.AddScoped<LocalPosUnitOfWork>();
        services.AddScoped<Application.Common.Interfaces.ILocalPosUnitOfWork>(sp => sp.GetRequiredService<LocalPosUnitOfWork>());
        services.AddScoped<Application.Common.Interfaces.IOutboxRepository>(sp => sp.GetRequiredService<LocalPosUnitOfWork>());
        services.AddScoped<Application.Outbox.IOutboxRepository, OutboxRepository>();

        // Connectivity & Health Evaluator
        services.AddSingleton<ConnectivityPolicyOptions>(new ConnectivityPolicyOptions());
        services.AddSingleton<ICheckInStore, InMemoryCheckInStore>();
        services.AddSingleton<IServerTimeProvider>(sp => new ServerTimeProvider(
            sp.GetRequiredService<IMonotonicClock>(),
            sp.GetRequiredService<ICheckInStore>(),
            options.DeviceId
        ));
        services.AddSingleton<IConnectivityStateEvaluator, ConnectivityStateEvaluator>();

        // POS Peripheral & UI Controllers, Sync & Use Cases
        services.AddScoped<Application.Common.Interfaces.IPrinterService, TestPrinterService>();
        services.AddScoped<IAuditEventLogger, InMemoryAuditEventLogger>();
        services.AddSingleton<CloudInboxProcessor>();
        services.AddScoped<ICloudSyncEngine, CloudSyncEngine>();
        services.AddScoped<CompletePosWorkflowUseCase>();
        services.AddScoped<PosTerminalUiController>();

        return services;
    }
}

public class ClientServicesOptions
{
    public string? LocalConnectionString { get; set; }
    public string DeviceId { get; set; } = "LOCAL_POS_DEVICE_01";
}
