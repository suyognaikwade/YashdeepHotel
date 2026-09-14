namespace Yashdeep.Application.Entitlements;

using Yashdeep.Shared.Capabilities;

public class ApiAuthorizationContext
{
    public object ActionDescriptor { get; set; } = new();
    public object? Result { get; set; }
    public IDictionary<string, object?> HttpContextItems { get; } = new Dictionary<string, object?>();
    public int StatusCode { get; set; } = 200;
}

public interface ICapabilityAuthorizationFilter
{
    void OnActionExecuting(ApiAuthorizationContext context, IEnumerable<RequireCapabilityAttribute> requiredCapabilities);
}

public class CapabilityAuthorizationFilter : ICapabilityAuthorizationFilter
{
    private readonly ICapabilityEvaluator _capabilityEvaluator;

    public CapabilityAuthorizationFilter(ICapabilityEvaluator capabilityEvaluator)
    {
        _capabilityEvaluator = capabilityEvaluator ?? throw new ArgumentNullException(nameof(capabilityEvaluator));
    }

    public void OnActionExecuting(ApiAuthorizationContext context, IEnumerable<RequireCapabilityAttribute> requiredCapabilities)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (requiredCapabilities == null)
        {
            return;
        }

        foreach (var attr in requiredCapabilities)
        {
            if (!_capabilityEvaluator.IsCapabilityEnabled(attr.CapabilityId))
            {
                context.StatusCode = 403;
                context.Result = new
                {
                    error = "capability_access_denied",
                    requiredCapability = attr.CapabilityId.Value,
                    message = $"API access denied. Active entitlement does not include capability '{attr.CapabilityId.Value}'."
                };
                return;
            }
        }
    }
}
