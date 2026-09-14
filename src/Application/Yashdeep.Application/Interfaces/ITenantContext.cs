namespace Yashdeep.Application.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid OrganizationId { get; }
    Guid UserId { get; }
    bool IsAuthenticated { get; }

    void SetContext(Guid tenantId, Guid organizationId, Guid userId);
}
