namespace Yashdeep.Tests;

using Yashdeep.Application.Entitlements;
using Yashdeep.Domain.Entitlements;
using Yashdeep.Infrastructure.Security;
using Yashdeep.Shared.Capabilities;
using Yashdeep.Shared.Editions;
using Yashdeep.Shared.Entitlements;
using Xunit;

public class CapabilityIntegrationTests
{
    private readonly Ed25519EntitlementTokenService _tokenService;
    private readonly Org.BouncyCastle.Crypto.Parameters.Ed25519PrivateKeyParameters _privateKey;
    private readonly string _publicKeyHex;

    public CapabilityIntegrationTests()
    {
        _tokenService = new Ed25519EntitlementTokenService();
        var keyPair = _tokenService.GenerateKeyPair();
        _privateKey = keyPair.PrivateKey;
        _publicKeyHex = Convert.ToHexString(keyPair.PublicKey.GetEncoded()).ToLowerInvariant();
    }

    private class TestTenantContext : IEntitlementTenantContext
    {
        public string? TenantId { get; set; }
    }

    [RequireCapability("pos_billing")]
    private class PosBillingCommand { }

    [RequireCapability("hotel_rooms")]
    private class HotelBookingCommand { }

    [Fact]
    public void CapabilityEnabled_BarAndRestaurantEdition_EnablesPosBilling()
    {
        var now = DateTime.UtcNow;
        var barCaps = EditionBundles.GetCapabilitiesForEdition(EditionBundles.BarAndRestaurantEdition);
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_bar_01",
            PlanCode = EditionBundles.BarAndRestaurantEdition,
            IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = barCaps.Select(c => c.Value).ToHashSet()
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);
        var tenantContext = new TestTenantContext { TenantId = "tenant_bar_01" };
        var evaluator = new CapabilityEvaluator(_tokenService, () => envelope, () => now, tenantContext);

        Assert.True(evaluator.IsCapabilityEnabled(CapabilityId.PosBilling));
        Assert.True(evaluator.IsCapabilityEnabled(CapabilityId.ExciseFl3Compliance));
        Assert.False(evaluator.IsCapabilityEnabled(CapabilityId.HotelRooms));
    }

    [Fact]
    public void CapabilityDisabled_HotelEdition_DisablesPosBilling()
    {
        var now = DateTime.UtcNow;
        var hotelCaps = EditionBundles.GetCapabilitiesForEdition(EditionBundles.HotelHospitalityEdition);
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_hotel_01",
            PlanCode = EditionBundles.HotelHospitalityEdition,
            IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = hotelCaps.Select(c => c.Value).ToHashSet()
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);
        var tenantContext = new TestTenantContext { TenantId = "tenant_hotel_01" };
        var evaluator = new CapabilityEvaluator(_tokenService, () => envelope, () => now, tenantContext);

        Assert.True(evaluator.IsCapabilityEnabled(CapabilityId.HotelRooms));
        Assert.False(evaluator.IsCapabilityEnabled(CapabilityId.PosBilling));
    }

    [Fact]
    public void MissingEntitlement_EnvelopeNull_ReturnsDisabled()
    {
        var evaluator = new CapabilityEvaluator(_tokenService, () => null);

        Assert.False(evaluator.IsCapabilityEnabled(CapabilityId.PosBilling));
        Assert.Empty(evaluator.GetActiveCapabilities());
    }

    [Fact]
    public void ExpiredEntitlement_PastOfflineGrace_ReturnsDisabled()
    {
        var now = DateTime.UtcNow;
        var past = now.AddDays(-30);
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_expired",
            IssuedAtUnix = ((DateTimeOffset)past.AddDays(-30)).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)past).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)past.AddDays(7)).ToUnixTimeSeconds(),
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);
        var evaluator = new CapabilityEvaluator(_tokenService, () => envelope, () => now);

        Assert.False(evaluator.IsCapabilityEnabled(CapabilityId.PosBilling));
        Assert.Empty(evaluator.GetActiveCapabilities());
    }

    [Fact]
    public void DependentCapabilityUnavailable_PrerequisiteMissing_ExcludesDependentCapability()
    {
        var now = DateTime.UtcNow;
        // Grant ExciseFl3Compliance without granting PosBilling or MultiTierInventory
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_missing_prereq",
            IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = new HashSet<string> { CapabilityId.ExciseFl3Compliance.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);
        var evaluator = new CapabilityEvaluator(_tokenService, () => envelope, () => now);

        Assert.False(evaluator.IsCapabilityEnabled(CapabilityId.ExciseFl3Compliance));
        Assert.DoesNotContain(CapabilityId.ExciseFl3Compliance, evaluator.GetActiveCapabilities());
    }

    [Fact]
    public void DomainBoundary_GuardThrowsCapabilityException_WhenCapabilityDisabled()
    {
        var now = DateTime.UtcNow;
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_domain_test",
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);
        var evaluator = new CapabilityEvaluator(_tokenService, () => envelope, () => now);

        var domainChecker = new DomainCapabilityCheckerAdapter(evaluator);

        // Guard should succeed for enabled capability
        DomainCapabilityGuard.EnforceCapability(domainChecker, CapabilityId.PosBilling);

        // Guard should throw CapabilityException for disabled capability
        var ex = Assert.Throws<CapabilityException>(() =>
            DomainCapabilityGuard.EnforceCapability(domainChecker, CapabilityId.HotelRooms));

        Assert.Equal(CapabilityId.HotelRooms, ex.RequiredCapability);
    }

    [Fact]
    public async Task ApplicationCommandDenied_WhenRequiredCapabilityMissing()
    {
        var now = DateTime.UtcNow;
        // Tenant only has HotelRooms, attempting PosBillingCommand
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_app_test",
            Capabilities = new HashSet<string> { CapabilityId.HotelRooms.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);
        var evaluator = new CapabilityEvaluator(_tokenService, () => envelope, () => now);
        var behavior = new CapabilityAuthorizationBehavior<PosBillingCommand, bool>(evaluator);

        var ex = await Assert.ThrowsAsync<CapabilityException>(async () =>
        {
            await behavior.Handle(new PosBillingCommand(), () => Task.FromResult(true), CancellationToken.None);
        });

        Assert.Equal(CapabilityId.PosBilling, ex.RequiredCapability);
    }

    [Fact]
    public void ApiAccessDenied_DirectCallBypass_Returns403Forbidden()
    {
        var now = DateTime.UtcNow;
        // Malicious client calls Hotel API endpoint directly without HotelRooms capability
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_malicious",
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);
        var evaluator = new CapabilityEvaluator(_tokenService, () => envelope, () => now);
        var apiFilter = new CapabilityAuthorizationFilter(evaluator);

        var apiContext = new ApiAuthorizationContext();
        var requiredAttrs = new[] { new RequireCapabilityAttribute("hotel_rooms") };

        apiFilter.OnActionExecuting(apiContext, requiredAttrs);

        Assert.Equal(403, apiContext.StatusCode);
        Assert.NotNull(apiContext.Result);
    }

    [Fact]
    public void UiVisibility_ReflectsCapabilitiesWithoutBeingSecurityBoundary()
    {
        var now = DateTime.UtcNow;
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_ui_test",
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);
        var evaluator = new CapabilityEvaluator(_tokenService, () => envelope, () => now);
        var uiHelper = new CapabilityViewHelper(evaluator);

        // UI shows POS feature, hides Hotel feature
        Assert.True(uiHelper.IsFeatureVisible(CapabilityId.PosBilling));
        Assert.False(uiHelper.IsFeatureVisible(CapabilityId.HotelRooms));
    }

    [Fact]
    public void EntitlementCaching_And_CacheInvalidation_AfterRefresh()
    {
        var now = DateTime.UtcNow;
        var cache = new EntitlementCache();
        var tenantContext = new TestTenantContext { TenantId = "tenant_cache_01" };

        var initialPayload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_cache_01",
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };
        var initialEnvelope = _tokenService.SignTokenPayload(initialPayload, _privateKey);

        var evaluator = new CapabilityEvaluator(
            _tokenService,
            currentEnvelopeProvider: () => initialEnvelope,
            currentTimeProvider: () => now,
            tenantContext: tenantContext,
            entitlementCache: cache);

        // Initial evaluation populates cache
        Assert.True(evaluator.IsCapabilityEnabled(CapabilityId.PosBilling));
        Assert.False(evaluator.IsCapabilityEnabled(CapabilityId.HotelRooms));
        Assert.NotNull(cache.GetCachedEnvelope("tenant_cache_01"));

        // Simulate entitlement refresh by invalidating cache and updating payload
        cache.InvalidateCache("tenant_cache_01");
        Assert.Null(cache.GetCachedEnvelope("tenant_cache_01"));

        var refreshedCaps = EditionBundles.GetCapabilitiesForEdition(EditionBundles.HotelBarRestaurantHybridEdition);
        var refreshedPayload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_cache_01",
            PlanCode = EditionBundles.HotelBarRestaurantHybridEdition,
            IssuedAtUnix = ((DateTimeOffset)now).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = refreshedCaps.Select(c => c.Value).ToHashSet()
        };
        var refreshedEnvelope = _tokenService.SignTokenPayload(refreshedPayload, _privateKey);

        var refreshedEvaluator = new CapabilityEvaluator(
            _tokenService,
            currentEnvelopeProvider: () => refreshedEnvelope,
            currentTimeProvider: () => now,
            tenantContext: tenantContext,
            entitlementCache: cache);

        // Post-refresh evaluation reflects new hybrid capabilities
        Assert.True(refreshedEvaluator.IsCapabilityEnabled(CapabilityId.PosBilling));
        Assert.True(refreshedEvaluator.IsCapabilityEnabled(CapabilityId.HotelRooms));
        Assert.NotNull(cache.GetCachedEnvelope("tenant_cache_01"));
    }

    [Fact]
    public void TenantContext_MismatchWithToken_ReturnsDisabled()
    {
        var now = DateTime.UtcNow;
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_A",
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);
        // Active tenant context is tenant_B, but token payload is for tenant_A
        var tenantContext = new TestTenantContext { TenantId = "tenant_B" };

        var evaluator = new CapabilityEvaluator(
            _tokenService,
            currentEnvelopeProvider: () => envelope,
            currentTimeProvider: () => now,
            tenantContext: tenantContext);

        Assert.False(evaluator.IsCapabilityEnabled(CapabilityId.PosBilling));
        Assert.Empty(evaluator.GetActiveCapabilities());
    }

    [Fact]
    public void VerifySignature_WithUntrustedPublicKeyInEnvelope_RejectedWhenDefaultKeyConfigured()
    {
        var now = DateTime.UtcNow;
        // Trusted service configured with official server public key
        var trustedService = new Ed25519EntitlementTokenService(_publicKeyHex);

        // Attacker generates an arbitrary key pair
        var (attackerPrivKey, _) = _tokenService.GenerateKeyPair();
        var forgedPayload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_forged",
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value, CapabilityId.HotelRooms.Value }
        };

        // Attacker signs payload with attacker private key and embeds attacker public key in envelope
        var forgedEnvelope = _tokenService.SignTokenPayload(forgedPayload, attackerPrivKey);

        // Evaluation against trusted service MUST fail verification because attacker key != trusted key
        Assert.False(trustedService.VerifySignature(forgedEnvelope));

        var evaluator = new CapabilityEvaluator(trustedService, () => forgedEnvelope, () => now);
        Assert.False(evaluator.IsCapabilityEnabled(CapabilityId.PosBilling));
        Assert.False(evaluator.IsCapabilityEnabled(CapabilityId.HotelRooms));
        Assert.Empty(evaluator.GetActiveCapabilities());
    }

    [Fact]
    public void Evaluate_FutureNotBeforeToken_ReturnsEmptyCapabilities()
    {
        var now = DateTime.UtcNow;
        var future = now.AddDays(5);
        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_future",
            IssuedAtUnix = ((DateTimeOffset)future).ToUnixTimeSeconds(),
            NotBeforeUnix = ((DateTimeOffset)future).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)future.AddDays(30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)future.AddDays(37)).ToUnixTimeSeconds(),
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };

        var envelope = _tokenService.SignTokenPayload(payload, _privateKey);
        var evaluator = new CapabilityEvaluator(_tokenService, () => envelope, () => now);

        Assert.False(evaluator.IsCapabilityEnabled(CapabilityId.PosBilling));
        Assert.Empty(evaluator.GetActiveCapabilities());
    }

    [Fact]
    public void EntitlementCache_StaleOrExpiredEnvelopeInCache_AutomaticallyEvictedAndInvalidated()
    {
        var now = DateTime.UtcNow;
        var cache = new EntitlementCache();
        var tenantContext = new TestTenantContext { TenantId = "tenant_stale_cache" };

        var expiredPayload = new SignedEntitlementTokenPayload
        {
            TenantId = "tenant_stale_cache",
            IssuedAtUnix = ((DateTimeOffset)now.AddDays(-60)).ToUnixTimeSeconds(),
            ExpirationUnix = ((DateTimeOffset)now.AddDays(-30)).ToUnixTimeSeconds(),
            OfflineGraceExpirationUnix = ((DateTimeOffset)now.AddDays(-20)).ToUnixTimeSeconds(),
            Capabilities = new HashSet<string> { CapabilityId.PosBilling.Value }
        };
        var expiredEnvelope = _tokenService.SignTokenPayload(expiredPayload, _privateKey);

        // Manually insert expired envelope into cache
        cache.CacheEnvelope("tenant_stale_cache", expiredEnvelope);
        Assert.NotNull(cache.GetCachedEnvelope("tenant_stale_cache"));

        var evaluator = new CapabilityEvaluator(
            _tokenService,
            currentEnvelopeProvider: () => null,
            currentTimeProvider: () => now,
            tenantContext: tenantContext,
            entitlementCache: cache);

        // Evaluation detects expired cached token, returns false and evicts from cache
        Assert.False(evaluator.IsCapabilityEnabled(CapabilityId.PosBilling));
        Assert.Null(cache.GetCachedEnvelope("tenant_stale_cache"));
    }

    private class DomainCapabilityCheckerAdapter : IDomainCapabilityChecker
    {
        private readonly ICapabilityEvaluator _evaluator;

        public DomainCapabilityCheckerAdapter(ICapabilityEvaluator evaluator)
        {
            _evaluator = evaluator;
        }

        public bool IsCapabilityEnabled(CapabilityId capabilityId)
        {
            return _evaluator.IsCapabilityEnabled(capabilityId);
        }
    }
}
