namespace Yashdeep.Domain.Entitlements;

using Yashdeep.Shared.Capabilities;

public sealed class Capability
{
    public CapabilityId Id { get; }
    public string Name { get; }
    public string Description { get; }

    public Capability(CapabilityId id, string name, string description)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
    }
}

public enum SubscriptionStatus
{
    Trial,
    Active,
    GracePeriod,
    Suspended,
    Terminated
}

public sealed class SubscriptionPlan
{
    public string PlanId { get; }
    public string Code { get; }
    public string Name { get; }

    private readonly HashSet<CapabilityId> _baseCapabilities = new();
    public IReadOnlySet<CapabilityId> BaseCapabilities => _baseCapabilities;

    public SubscriptionPlan(string planId, string code, string name, IEnumerable<CapabilityId> baseCapabilities)
    {
        PlanId = planId ?? throw new ArgumentNullException(nameof(planId));
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Name = name ?? throw new ArgumentNullException(nameof(name));

        if (baseCapabilities != null)
        {
            foreach (var cap in baseCapabilities)
            {
                _baseCapabilities.Add(cap);
            }
        }
    }
}

public enum EntitlementOverrideType
{
    Grant,
    Revoke
}

public sealed class EntitlementOverride
{
    public CapabilityId CapabilityId { get; }
    public EntitlementOverrideType OverrideType { get; }
    public string Reason { get; }

    public EntitlementOverride(CapabilityId capabilityId, EntitlementOverrideType overrideType, string reason = "")
    {
        CapabilityId = capabilityId;
        OverrideType = overrideType;
        Reason = reason ?? string.Empty;
    }
}

public sealed class TenantEntitlementAggregate
{
    public string TenantId { get; }
    public SubscriptionPlan Plan { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime UtcExpiresAt { get; private set; }
    public DateTime UtcOfflineGraceExpiresAt { get; private set; }

    private readonly List<EntitlementOverride> _overrides = new();
    public IReadOnlyCollection<EntitlementOverride> Overrides => _overrides.AsReadOnly();

    public TenantEntitlementAggregate(
        string tenantId,
        SubscriptionPlan plan,
        SubscriptionStatus status,
        DateTime utcExpiresAt,
        DateTime utcOfflineGraceExpiresAt)
    {
        TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        Status = status;
        UtcExpiresAt = utcExpiresAt;
        UtcOfflineGraceExpiresAt = utcOfflineGraceExpiresAt;
    }

    public void AddOverride(EntitlementOverride entitlementOverride)
    {
        ArgumentNullException.ThrowIfNull(entitlementOverride);
        _overrides.RemoveAll(o => o.CapabilityId == entitlementOverride.CapabilityId);
        _overrides.Add(entitlementOverride);
    }

    public void RemoveOverride(CapabilityId capabilityId)
    {
        _overrides.RemoveAll(o => o.CapabilityId == capabilityId);
    }

    public void UpdateSubscriptionStatus(SubscriptionStatus newStatus)
    {
        Status = newStatus;
    }

    /// <summary>
    /// Computes active capabilities enforcing plan baseline, overrides, DAG dependency validation, and entitlement status invariants.
    /// Invariant: Disabling one capability cannot accidentally enable another.
    /// Invariant: Disabling a prerequisite capability automatically disables all dependent capabilities.
    /// Invariant: Suspended or Terminated subscription status evaluates to zero active capabilities.
    /// </summary>
    public IReadOnlySet<CapabilityId> EvaluateActiveCapabilities(DateTime currentUtcTime)
    {
        if (Status == SubscriptionStatus.Suspended || Status == SubscriptionStatus.Terminated)
        {
            return new HashSet<CapabilityId>();
        }

        if (currentUtcTime > UtcOfflineGraceExpiresAt)
        {
            return new HashSet<CapabilityId>();
        }

        var candidateCapabilities = new HashSet<CapabilityId>(Plan.BaseCapabilities);

        foreach (var over in _overrides)
        {
            if (over.OverrideType == EntitlementOverrideType.Grant)
            {
                candidateCapabilities.Add(over.CapabilityId);
            }
            else if (over.OverrideType == EntitlementOverrideType.Revoke)
            {
                candidateCapabilities.Remove(over.CapabilityId);
            }
        }

        return CapabilityDependencyMap.ResolveValidCapabilities(candidateCapabilities);
    }
}
