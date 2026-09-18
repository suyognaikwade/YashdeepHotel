namespace Yashdeep.Application.Entitlements;

using Yashdeep.Shared.Capabilities;
using Yashdeep.Shared.Entitlements;

public interface ICapabilityEvaluator
{
    bool IsCapabilityEnabled(CapabilityId capabilityId);
    bool IsCapabilityEnabled(SignedEntitlementEnvelope envelope, CapabilityId capabilityId, DateTime currentUtc);
    IReadOnlySet<CapabilityId> GetActiveCapabilities();
    IReadOnlySet<CapabilityId> GetActiveCapabilities(SignedEntitlementEnvelope envelope, DateTime currentUtc);
    EntitlementCapacityLimits GetCapacityLimits();
    EntitlementCapacityLimits GetCapacityLimits(SignedEntitlementEnvelope envelope);
}

public interface IEntitlementTokenParser
{
    SignedEntitlementTokenPayload? ParsePayload(SignedEntitlementEnvelope envelope);
}
