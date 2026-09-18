namespace Yashdeep.Application.Entitlements;

using System.Reflection;
using Yashdeep.Domain.Entitlements;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : notnull
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

public class CapabilityAuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICapabilityEvaluator _capabilityEvaluator;

    public CapabilityAuthorizationBehavior(ICapabilityEvaluator capabilityEvaluator)
    {
        _capabilityEvaluator = capabilityEvaluator ?? throw new ArgumentNullException(nameof(capabilityEvaluator));
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var attributes = request.GetType().GetCustomAttributes<RequireCapabilityAttribute>(inherit: true);

        foreach (var attr in attributes)
        {
            if (!_capabilityEvaluator.IsCapabilityEnabled(attr.CapabilityId))
            {
                throw new CapabilityException(attr.CapabilityId, $"Application request '{typeof(TRequest).Name}' rejected. Required capability '{attr.CapabilityId}' is not active.");
            }
        }

        return await next();
    }
}
