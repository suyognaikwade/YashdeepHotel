using MediatR;
using Yashdeep.Domain.Contexts;
using Yashdeep.Domain.Contracts;
using Yashdeep.Domain.Exceptions;

namespace Yashdeep.Application.Behaviors;

public class RequestAuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IRequestContext _requestContext;

    public RequestAuthorizationBehavior(IRequestContext requestContext)
    {
        _requestContext = requestContext;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is ITenantEntity tenantEntity)
        {
            if (!_requestContext.Tenant.IsTenantResolved)
            {
                throw new TenantAuthorizationException("Tenant context is missing or not resolved.");
            }

            if (tenantEntity.TenantId != Guid.Empty && tenantEntity.TenantId != _requestContext.Tenant.TenantId)
            {
                throw new TenantAuthorizationException($"Tenant ID mismatch in request payload. Request: {tenantEntity.TenantId}, Context: {_requestContext.Tenant.TenantId}");
            }
        }

        if (request is IBranchEntity branchEntity)
        {
            if (!_requestContext.Branch.IsBranchResolved && !_requestContext.Branch.IsOrganizationAccess && !_requestContext.User.IsSystemAdmin)
            {
                throw new BranchAuthorizationException("Branch context is required for branch-scoped requests.");
            }

            if (branchEntity.BranchId != Guid.Empty &&
                !_requestContext.User.IsSystemAdmin &&
                !_requestContext.Branch.IsOrganizationAccess &&
                !_requestContext.Branch.AllowedBranchIds.Contains(branchEntity.BranchId))
            {
                throw new BranchAuthorizationException($"User is not authorized for branch {branchEntity.BranchId}.");
            }
        }

        return await next();
    }
}
