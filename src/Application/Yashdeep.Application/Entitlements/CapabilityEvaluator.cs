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

    public CapabilityEvaluator(
        IEntitlementTokenVerifier tokenVerifier,
        Func<SignedEntitlementEnvelope?>? currentEnvelopeProvider = null,
        Func<DateTime>? currentTimeProvider = null)
    {
        _tokenVerifier = tokenVerifier ?? throw new ArgumentNullException(nameof(tokenVerifier));
        _currentEnvelopeProvider = currentEnvelopeProvider;
        _currentTimeProvider = currentTimeProvider ?? (() => DateTime.UtcNow);
    }

    public bool IsCapabilityEnabled(CapabilityId capabilityId)
    {
        var envelope = _currentEnvelopeProvider?.Invoke();
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
        var envelope = _currentEnvelopeProvider?.Invoke();
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
        var envelope = _currentEnvelopeProvider?.Invoke();
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
}
