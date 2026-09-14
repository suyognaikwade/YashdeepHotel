namespace Yashdeep.Domain.Entitlements;

using Yashdeep.Shared.Capabilities;

public class CapabilityException : Exception
{
    public CapabilityId RequiredCapability { get; }

    public CapabilityException(CapabilityId requiredCapability)
        : base($"Domain invariant violation: Required capability '{requiredCapability}' is not enabled for the current context.")
    {
        RequiredCapability = requiredCapability;
    }

    public CapabilityException(CapabilityId requiredCapability, string message)
        : base(message)
    {
        RequiredCapability = requiredCapability;
    }
}
