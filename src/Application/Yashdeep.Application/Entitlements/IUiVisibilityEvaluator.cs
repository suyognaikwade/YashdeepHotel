namespace Yashdeep.Application.Entitlements;

using Yashdeep.Shared.Capabilities;

public interface IUiVisibilityEvaluator
{
    bool IsFeatureVisible(CapabilityId capabilityId);
    bool AreAllFeaturesVisible(params CapabilityId[] capabilityIds);
    bool IsAnyFeatureVisible(params CapabilityId[] capabilityIds);
}

public class CapabilityViewHelper : IUiVisibilityEvaluator
{
    private readonly ICapabilityEvaluator _capabilityEvaluator;

    public CapabilityViewHelper(ICapabilityEvaluator capabilityEvaluator)
    {
        _capabilityEvaluator = capabilityEvaluator ?? throw new ArgumentNullException(nameof(capabilityEvaluator));
    }

    public bool IsFeatureVisible(CapabilityId capabilityId)
    {
        return _capabilityEvaluator.IsCapabilityEnabled(capabilityId);
    }

    public bool AreAllFeaturesVisible(params CapabilityId[] capabilityIds)
    {
        if (capabilityIds == null || capabilityIds.Length == 0)
        {
            return true;
        }

        foreach (var cap in capabilityIds)
        {
            if (!_capabilityEvaluator.IsCapabilityEnabled(cap))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsAnyFeatureVisible(params CapabilityId[] capabilityIds)
    {
        if (capabilityIds == null || capabilityIds.Length == 0)
        {
            return false;
        }

        foreach (var cap in capabilityIds)
        {
            if (_capabilityEvaluator.IsCapabilityEnabled(cap))
            {
                return true;
            }
        }

        return false;
    }
}
