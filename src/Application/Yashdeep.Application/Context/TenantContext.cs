using Yashdeep.Application.Interfaces;

namespace Yashdeep.Application.Context;

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsAuthenticated => TenantId != Guid.Empty && UserId != Guid.Empty;

    public void SetContext(Guid tenantId, Guid organizationId, Guid userId)
    {
        TenantId = tenantId;
        OrganizationId = organizationId;
        UserId = userId;
    }
}
