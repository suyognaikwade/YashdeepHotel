using Microsoft.Extensions.DependencyInjection;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Application.Connectivity;
using Yashdeep.Application.Entitlements;
using Yashdeep.Application.Outbox;
using Yashdeep.Application.Persistence;
using Yashdeep.Application.Pos.UI;
using Yashdeep.Client.Blazor.Components;
using Yashdeep.Client.Blazor.DependencyInjection;
using Yashdeep.Domain.Connectivity;
using Yashdeep.Infrastructure.Security;
using Yashdeep.Shared.Capabilities;
using Yashdeep.Shared.Entitlements;
using Xunit;

namespace Yashdeep.Tests;

public class ClientFoundationTests
{
    [Fact]
    public void AddYashdeepClientServices_ResolvesAllRequiredClientServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddYashdeepClientServices(opts =>
        {
            opts.LocalConnectionString = "Data Source=file::memory:?mode=memory&cache=shared";
        });

        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<IClockTamperDetector>());
        Assert.NotNull(provider.GetService<IEntitlementCache>());
        Assert.NotNull(provider.GetService<ICapabilityEvaluator>());
        Assert.NotNull(provider.GetService<IUiVisibilityEvaluator>());
        Assert.NotNull(provider.GetService<IConnectivityStateEvaluator>());
        Assert.NotNull(provider.GetService<ILocalPosUnitOfWork>());
        Assert.NotNull(provider.GetService<Yashdeep.Application.Outbox.IOutboxRepository>());
        Assert.NotNull(provider.GetService<PosTerminalUiController>());
    }

    [Fact]
    public void ClientAssemblies_DoNotReferenceCloudPersistenceAssembly()
    {
        // Arrange
        var clientAssembly = typeof(ClientServiceCollectionExtensions).Assembly;

        // Act
        var referencedAssemblies = clientAssembly.GetReferencedAssemblies();

        // Assert
        Assert.DoesNotContain(referencedAssemblies, a => a.Name != null && a.Name.Contains("Persistence.Cloud"));
        Assert.DoesNotContain(referencedAssemblies, a => a.Name != null && a.Name.Contains("Npgsql"));
    }

    [Fact]
    public void PosKeyboardAction_DefinesExpectedShortcutKeys()
    {
        // Act & Assert
        Assert.Equal(PosKeyboardAction.NewOrder, Enum.Parse<PosKeyboardAction>("NewOrder"));
        Assert.Equal(PosKeyboardAction.SettlePayment, Enum.Parse<PosKeyboardAction>("SettlePayment"));
    }

    [Fact]
    public void UiVisibilityEvaluator_EvaluatesCapabilityVisibilityCorrectly()
    {
        // Arrange
        var tokenService = new Ed25519EntitlementTokenService();
        var keyPair = tokenService.GenerateKeyPair();

        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "TENANT_01",
            SubscriptionStatus = "Active",
            Capabilities = new HashSet<string> { "POS.Core", "POS.TableManagement" },
            OfflineGraceExpirationUnix = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds()
        };

        var envelope = tokenService.SignTokenPayload(payload, keyPair.PrivateKey);

        var evaluator = new CapabilityEvaluator(
            tokenVerifier: tokenService,
            currentEnvelopeProvider: () => envelope
        );

        var visibilityEvaluator = new CapabilityViewHelper(evaluator);

        // Act & Assert
        Assert.True(visibilityEvaluator.IsFeatureVisible(new CapabilityId("POS.Core")));
        Assert.False(visibilityEvaluator.IsFeatureVisible(new CapabilityId("HOTEL.NightAudit")));
    }
}
