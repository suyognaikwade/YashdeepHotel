namespace Yashdeep.Application.Entitlements;

using Yashdeep.Shared.Capabilities;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class RequireCapabilityAttribute : Attribute
{
    public CapabilityId CapabilityId { get; }

    public RequireCapabilityAttribute(string capabilityValue)
    {
        CapabilityId = new CapabilityId(capabilityValue);
    }
}
