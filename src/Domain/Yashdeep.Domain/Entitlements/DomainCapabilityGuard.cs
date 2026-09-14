namespace Yashdeep.Domain.Entitlements;

using Yashdeep.Shared.Capabilities;

public interface IDomainCapabilityChecker
{
    bool IsCapabilityEnabled(CapabilityId capabilityId);
}

public static class DomainCapabilityGuard
{
    public static void EnforceCapability(IDomainCapabilityChecker capabilityChecker, CapabilityId requiredCapability)
    {
        ArgumentNullException.ThrowIfNull(capabilityChecker);

        if (!capabilityChecker.IsCapabilityEnabled(requiredCapability))
        {
            throw new CapabilityException(requiredCapability);
        }
    }
}
