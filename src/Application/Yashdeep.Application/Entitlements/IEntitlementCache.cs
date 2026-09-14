namespace Yashdeep.Application.Entitlements;

using Yashdeep.Shared.Entitlements;

public interface IEntitlementStore
{
    SignedEntitlementEnvelope? GetEnvelopeForTenant(string tenantId);
}

public interface IEntitlementCache
{
    SignedEntitlementEnvelope? GetCachedEnvelope(string tenantId);
    void CacheEnvelope(string tenantId, SignedEntitlementEnvelope envelope);
    void InvalidateCache(string tenantId);
}
