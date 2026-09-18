using Yashdeep.Domain.Entities;

namespace Yashdeep.Application.Services;

public interface ITenantValidationService
{
    Task<Tenant?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<User?> GetUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsBranchAuthorizedForUserAsync(Guid tenantId, Guid userId, Guid branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> GetAuthorizedBranchIdsForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}

public class InMemoryTenantValidationService : ITenantValidationService
{
    private readonly List<Tenant> _tenants = new();
    private readonly List<User> _users = new();

    public InMemoryTenantValidationService(IEnumerable<Tenant>? tenants = null, IEnumerable<User>? users = null)
    {
        if (tenants != null) _tenants.AddRange(tenants);
        if (users != null) _users.AddRange(users);
    }

    public void AddTenant(Tenant tenant) => _tenants.Add(tenant);
    public void AddUser(User user) => _users.Add(user);

    public Task<Tenant?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = _tenants.FirstOrDefault(t => t.Id == tenantId && t.IsActive);
        return Task.FromResult(tenant);
    }

    public Task<User?> GetUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = _users.FirstOrDefault(u => u.TenantId == tenantId && u.Id == userId && u.IsActive);
        return Task.FromResult(user);
    }

    public Task<bool> IsBranchAuthorizedForUserAsync(Guid tenantId, Guid userId, Guid branchId, CancellationToken cancellationToken = default)
    {
        var user = _users.FirstOrDefault(u => u.TenantId == tenantId && u.Id == userId && u.IsActive);
        if (user == null) return Task.FromResult(false);
        if (user.IsSystemAdmin || user.HasOrganizationWideAccess) return Task.FromResult(true);

        var isAssigned = user.BranchAssignments.Any(ba => ba.BranchId == branchId);
        return Task.FromResult(isAssigned);
    }

    public Task<IReadOnlyCollection<Guid>> GetAuthorizedBranchIdsForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = _users.FirstOrDefault(u => u.TenantId == tenantId && u.Id == userId && u.IsActive);
        if (user == null) return Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());

        if (user.IsSystemAdmin || user.HasOrganizationWideAccess)
        {
            var tenant = _tenants.FirstOrDefault(t => t.Id == tenantId);
            if (tenant != null)
            {
                var allBranchIds = tenant.Branches.Select(b => b.Id).ToList();
                return Task.FromResult<IReadOnlyCollection<Guid>>(allBranchIds);
            }
        }

        var assignedBranchIds = user.BranchAssignments.Select(ba => ba.BranchId).ToList();
        return Task.FromResult<IReadOnlyCollection<Guid>>(assignedBranchIds);
    }
}
