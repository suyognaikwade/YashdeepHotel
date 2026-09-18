namespace Yashdeep.Application.Entitlements;

using System.Collections.Concurrent;
using Yashdeep.Shared.Entitlements;

public class EntitlementCache : IEntitlementCache
{
    private readonly ConcurrentDictionary<string, SignedEntitlementEnvelope> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SignedEntitlementEnvelope? GetCachedEnvelope(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        return _cache.TryGetValue(tenantId, out var envelope) ? envelope : null;
    }

    public void CacheEnvelope(string tenantId, SignedEntitlementEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || envelope == null)
        {
            return;
        }

        _cache[tenantId] = envelope;
    }

    public void InvalidateCache(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        _cache.TryRemove(tenantId, out _);
    }
}
