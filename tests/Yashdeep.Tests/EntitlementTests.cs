namespace Yashdeep.Tests;

using System.Text.Json;
using Yashdeep.Application.Entitlements;
using Yashdeep.Domain.Entitlements;
using Yashdeep.Infrastructure.Security;
using Yashdeep.Shared.Capabilities;
using Yashdeep.Shared.Editions;
using Yashdeep.Shared.Entitlements;
using Xunit;

public class EntitlementTests
{
    private readonly Ed25519EntitlementTokenService _tokenService;
    private readonly CapabilityEvaluator _evaluator;
    private readonly Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters _privateKey;
    private readonly string _publicKeyHex;

    public EntitlementTests()
    {
        _tokenService = new Ed25519EntitlementTokenService();
        var keyPair = _tokenService.GenerateKeyPair();
        _privateKey = keyPair.PrivateKey;
        _publicKeyHex = Convert.ToHexString(keyPair.PublicKey.GetEncoded()).ToLowerInvariant();
        _evaluator = new CapabilityEvaluator(_tokenService);
    }

    [Fact]
    public void Evaluate_BarAndRestaurantEdition_EnablesExpectedCapabilities()
    {
        // Arrange
        var barCapSet = EditionBundles.GetCapabilitiesForEdition(EditionBundles.BarAndRestaurantEdition);
        var now = DateTime.UtcNow;
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_bar_01",
            PlanCode = EditionBundles.BarAndRestaurantEdition,
            IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = barCapSet.Select(c => c.Value).ToHashSet()
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);

        // Act
        var activeCaps = _evaluator.GetActiveCapabilities(envelope, now);

        // Assert
        Assert.Contains(CapabilityId.PosBilling, activeCaps);
        Assert.Contains(CapabilityId.KotRouting, activeCaps);
        Assert.Contains(CapabilityId.MultiTierInventory, activeCaps);
        Assert.Contains(CapabilityId.ExciseFl3Compliance, activeCaps);
        Assert.Contains(CapabilityId.LoosePegDispensing, activeCaps);
        Assert.DoesNotContain(CapabilityId.HotelRooms, activeCaps);
    }

    [Fact]
    public void Evaluate_HotelHospitalityEdition_EnablesExpectedCapabilities()
    {
        // Arrange
        var hotelCapSet = EditionBundles.GetCapabilitiesForEdition(EditionBundles.HotelHospitalityEdition);
        var now = DateTime.UtcNow;
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_hotel_01",
            PlanCode = EditionBundles.HotelHospitalityEdition,
            IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = hotelCapSet.Select(c => c.Value).ToHashSet()
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);

        // Act
        var activeCaps = _evaluator.GetActiveCapabilities(envelope, now);

        // Assert
        Assert.Contains(CapabilityId.HotelRooms, activeCaps);
        Assert.Contains(CapabilityId.Housekeeping, activeCaps);
        Assert.DoesNotContain(CapabilityId.PosBilling, activeCaps);
        Assert.DoesNotContain(CapabilityId.ExciseFl3Compliance, activeCaps);
    }

    [Fact]
    public void Evaluate_HotelBarRestaurantHybridEdition_EnablesAllCombinedCapabilities()
    {
        // Arrange
        var hybridCapSet = EditionBundles.GetCapabilitiesForEdition(EditionBundles.HotelBarRestaurantHybridEdition);
        var now = DateTime.UtcNow;
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_hybrid_01",
            PlanCode = EditionBundles.HotelBarRestaurantHybridEdition,
            IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = hybridCapSet.Select(c => c.Value).ToHashSet()
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);

        // Act
        var activeCaps = _evaluator.GetActiveCapabilities(envelope, now);

        // Assert
        Assert.Contains(CapabilityId.PosBilling, activeCaps);
        Assert.Contains(CapabilityId.HotelRooms, activeCaps);
        Assert.Contains(CapabilityId.RoomServicePosting, activeCaps);
        Assert.Contains(CapabilityId.ExciseFl3Compliance, activeCaps);
    }

    [Fact]
    public void Evaluate_InvalidSignature_ReturnsEmptyCapabilities()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_tampered",
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);
        // Tamper with payload
        envelope.PayloadJson = envelope.PayloadJson.Replace("tenant_tampered", "tenant_hacked");

        // Act
        var activeCaps = _evaluator.GetActiveCapabilities(envelope, now);

        // Assert
        Assert.Empty(activeCaps);
    }

    [Fact]
    public void Evaluate_ExpiredToken_ReturnsEmptyCapabilities()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var past = now.AddDays(-40);
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_expired",
            IssuedAtUnix = ((DateTimeOffset)past.AddDays(-30)).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)past).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)past.AddDays(7)).ToUnixTimeSeconds(),
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);

        // Act
        var activeCaps = _evaluator.GetActiveCapabilities(envelope, now);

        // Assert
        Assert.Empty(activeCaps);
    }

    [Fact]
    public void Evaluate_SuspendedStatus_ReturnsEmptyCapabilities()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_suspended",
            SubscriptionStatus = "Suspended",
            IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);

        // Act
        var activeCaps = _evaluator.GetActiveCapabilities(envelope, now);

        // Assert
        Assert.Empty(activeCaps);
    }

    [Fact]
    public void Evaluate_MissingPrerequisite_ExcludesDependentCapability()
    {
        // Arrange
        var now = DateTime.UtcNow;
        // Request ExciseFl3Compliance WITHOUT requesting MultiTierInventory or PosBilling
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_missing_prereq",
            IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = new HashSet<string> { CapabilityId.ExciseFl3Compliance.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);

        // Act
        var activeCaps = _evaluator.GetActiveCapabilities(envelope, now);

        // Assert
        Assert.DoesNotContain(CapabilityId.ExciseFl3Compliance, activeCaps);
    }

    [Fact]
    public void Invariant_DisablingOneCapability_CannotEnableAnotherCapability()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var initialCaps = new HashSet<string>
        {
            CapabilityId.PosBilling.Value,
            CapabilityId.MultiTierInventory.Value,
            CapabilityId.ExciseFl3Compliance.Value
        };

        var payloadInitial = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_isolation",
            IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = initialCaps
        };

        var envInitial = _tokenService.SignTokenPayload(payloadInitial, _privateKey);
        var activeInitial = _evaluator.GetActiveCapabilities(envInitial, now);

        // Act - Disable MultiTierInventory
        var modifiedCaps = new HashSet<string>
        {
            CapabilityId.PosBilling.Value,
            CapabilityId.ExciseFl3Compliance.Value
        };

        var payloadModified = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_isolation",
            IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = modifiedCaps
        };

        var envModified = _tokenService.SignTokenPayload(payloadModified, _privateKey);
        var activeModified = _evaluator.GetActiveCapabilities(envModified, now);

        // Assert
        Assert.Contains(CapabilityId.ExciseFl3Compliance, activeInitial);
        // Since MultiTierInventory was removed, ExciseFl3Compliance must automatically be deactivated
        Assert.DoesNotContain(CapabilityId.ExciseFl3Compliance, activeModified);
        // And no unrelated capability (e.g. HotelRooms) was enabled
        Assert.DoesNotContain(CapabilityId.HotelRooms, activeModified);
    }

    [Fact]
    public void Evaluate_CustomTenantOverrides_AppliesGrantsAndRevocationsInDomainAggregate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var plan = new SubscriptionPlan(
            "plan_starter",
            "STARTER_POS",
            "Starter POS",
            new[] { CapabilityId.PosBilling, CapabilityId.KotRouting }
        );

        var tenantAggregate = new TenantEntitlementAggregate(
            "tenant_custom_overrides",
            plan,
            SubscriptionStatus.Active,
            now.AddDays(30),
            now.AddDays(37)
        );

        // Grant MultiTierInventory (with PosBilling already in plan)
        tenantAggregate.AddOverride(new EntitlementOverride(CapabilityId.MultiTierInventory, EntitlementOverrideType.Grant, "Custom Addon"));
        // Revoke KotRouting
        tenantAggregate.AddOverride(new EntitlementOverride(CapabilityId.KotRouting, EntitlementOverrideType.Revoke, "Opt out"));

        // Act
        var activeCaps = tenantAggregate.EvaluateActiveCapabilities(now);

        // Assert
        Assert.Contains(CapabilityId.PosBilling, activeCaps);
        Assert.Contains(CapabilityId.MultiTierInventory, activeCaps);
        Assert.DoesNotContain(CapabilityId.KotRouting, activeCaps);
    }

    [Fact]
    public void Cryptographic_Ed25519_TestVectors_VerifyCorrectly()
    {
        // Arrange
        var (privKey, pubKey) = _tokenService.GenerateKeyPair();
        string pubKeyHex = Convert.ToHexString(pubKey.GetEncoded()).ToLowerInvariant();

        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_test_vector",
            PlanCode = "PROFESSIONAL_BAR_REST",
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };

        // Act
        var envelope = _tokenService.SignTokenPayload(payload, privKey);
        bool validSelf = _tokenService.VerifySignature(envelope);
        bool validExplicitKey = _tokenService.VerifySignature(envelope, pubKeyHex);

        // Assert
        Assert.True(validSelf);
        Assert.True(validExplicitKey);

        // Verify with wrong key fails
        var (_, wrongPubKey) = _tokenService.GenerateKeyPair();
        string wrongPubKeyHex = Convert.ToHexString(wrongPubKey.GetEncoded()).ToLowerInvariant();
        Assert.False(_tokenService.VerifySignature(envelope, wrongPubKeyHex));
    }
}
