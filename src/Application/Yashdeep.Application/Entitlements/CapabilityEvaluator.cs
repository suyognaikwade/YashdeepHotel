namespace Yashdeep.Application.Entitlements;

using System.Text.Json;
using Yashdeep.Shared.Capabilities;
using Yashdeep.Shared.Entitlements;

public interface IEntitlementTokenVerifier
{
    bool VerifySignature(SignedEntitlementEnvelope envelope);
}

public sealed class CapabilityEvaluator : ICapabilityEvaluator, IEntitlementTokenParser
{
    private readonly IEntitlementTokenVerifier _tokenVerifier;
    private readonly Func<SignedEntitlementEnvelope?>? _currentEnvelopeProvider;
    private readonly Func<DateTime>? _currentTimeProvider;
    private readonly ITenantContext? _tenantContext;
    private readonly IEntitlementCache? _entitlementCache;
    private readonly IEntitlementStore? _entitlementStore;

    public CapabilityEvaluator(
        IEntitlementTokenVerifier tokenVerifier,
        Func<SignedEntitlementEnvelope?>? currentEnvelopeProvider = null,
        Func<DateTime>? currentTimeProvider = null,
        ITenantContext? tenantContext = null,
        IEntitlementCache? entitlementCache = null,
        IEntitlementStore? entitlementStore = null)
    {
        _tokenVerifier = tokenVerifier ?? throw new ArgumentNullException(nameof(tokenVerifier));
        _currentEnvelopeProvider = currentEnvelopeProvider;
        _currentTimeProvider = currentTimeProvider ?? (() => DateTime.UtcNow);
        _tenantContext = tenantContext;
        _entitlementCache = entitlementCache;
        _entitlementStore = entitlementStore;
    }

    public bool IsCapabilityEnabled(CapabilityId capabilityId)
    {
        var envelope = ResolveEnvelopeForCurrentContext();
        if (envelope == null)
        {
            return false;
        }

        return IsCapabilityEnabled(envelope, capabilityId, _currentTimeProvider!());
    }

    public bool IsCapabilityEnabled(SignedEntitlementEnvelope envelope, CapabilityId capabilityId, DateTime currentUtc)
    {
        var activeCapabilities = GetActiveCapabilities(envelope, currentUtc);
        return activeCapabilities.Contains(capabilityId);
    }

    public IReadOnlySet<CapabilityId> GetActiveCapabilities()
    {
        var envelope = ResolveEnvelopeForCurrentContext();
        if (envelope == null)
        {
            return new HashSet<CapabilityId>();
        }

        return GetActiveCapabilities(envelope, _currentTimeProvider!());
    }

    public IReadOnlySet<CapabilityId> GetActiveCapabilities(SignedEntitlementEnvelope envelope, DateTime currentUtc)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!_tokenVerifier.VerifySignature(envelope))
        {
            return new HashSet<CapabilityId>();
        }

        var payload = ParsePayload(envelope);
        if (payload == null)
        {
            return new HashSet<CapabilityId>();
        }

        // Validate Tenant Context if present
        if (_tenantContext != null && !string.IsNullOrWhiteSpace(_tenantContext.TenantId))
        {
            if (!string.Equals(_tenantContext.TenantId, payload.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                // Mismatched tenant context vs token tenant payload
                return new HashSet<CapabilityId>();
            }
        }

        if (payload.SubscriptionStatus.Equals("Suspended", StringComparison.OrdinalIgnoreCase) ||
            payload.SubscriptionStatus.Equals("Terminated", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<CapabilityId>();
        }

        long currentUnix = ((DateTimeOffset)currentUtc).ToUnixTimeSeconds();
        if (payload.OfflineGraceExpirationUnix > 0 && currentUnix > payload.OfflineGraceExpirationUnix)
        {
            return new HashSet<CapabilityId>();
        }

        var rawRequested = payload.Capabilities
            .Select(c => new CapabilityId(c))
            .ToHashSet();

        return CapabilityDependencyMap.ResolveValidCapabilities(rawRequested);
    }

    public EntitlementCapacityLimits GetCapacityLimits()
    {
        var envelope = ResolveEnvelopeForCurrentContext();
        if (envelope == null)
        {
            return new EntitlementCapacityLimits();
        }

        return GetCapacityLimits(envelope);
    }

    public EntitlementCapacityLimits GetCapacityLimits(SignedEntitlementEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!_tokenVerifier.VerifySignature(envelope))
        {
            return new EntitlementCapacityLimits();
        }

        var payload = ParsePayload(envelope);
        return payload?.Limits ?? new EntitlementCapacityLimits();
    }

    public SignedEntitlementTokenPayload? ParsePayload(SignedEntitlementEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SignedEntitlementTokenPayload>(envelope.PayloadJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private SignedEntitlementEnvelope? ResolveEnvelopeForCurrentContext()
    {
        var activeTenantId = _tenantContext?.TenantId;

        // Try cached envelope if active tenant context is known
        if (!string.IsNullOrWhiteSpace(activeTenantId) && _entitlementCache != null)
        {
            var cached = _entitlementCache.GetCachedEnvelope(activeTenantId);
            if (cached != null)
            {
                return cached;
            }
        }

        // Try current dynamic envelope provider
        var envelope = _currentEnvelopeProvider?.Invoke();

        // If not found in current envelope provider, try entitlement store
        if (envelope == null && !string.IsNullOrWhiteSpace(activeTenantId) && _entitlementStore != null)
        {
            envelope = _entitlementStore.GetEnvelopeForTenant(activeTenantId);
        }

        // Cache envelope if available
        if (envelope != null && !string.IsNullOrWhiteSpace(activeTenantId) && _entitlementCache != null)
        {
            _entitlementCache.CacheEnvelope(activeTenantId, envelope);
        }

        return envelope;
    }
}
