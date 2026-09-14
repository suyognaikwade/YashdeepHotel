using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(TenantId id, CancellationToken cancellationToken = default);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(OrganizationId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organization>> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);
    Task UpdateAsync(Organization organization, CancellationToken cancellationToken = default);
}

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(BranchId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Branch>> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Branch>> GetByOrganizationIdAsync(TenantId tenantId, OrganizationId organizationId, CancellationToken cancellationToken = default);
    Task AddAsync(Branch branch, CancellationToken cancellationToken = default);
    Task UpdateAsync(Branch branch, CancellationToken cancellationToken = default);
}

public interface IOutletRepository
{
    Task<Outlet?> GetByIdAsync(OutletId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Outlet>> GetByBranchIdAsync(TenantId tenantId, BranchId branchId, CancellationToken cancellationToken = default);
    Task AddAsync(Outlet outlet, CancellationToken cancellationToken = default);
    Task UpdateAsync(Outlet outlet, CancellationToken cancellationToken = default);
}

public interface ITerminalRepository
{
    Task<Terminal?> GetByIdAsync(TerminalId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Terminal>> GetByOutletIdAsync(TenantId tenantId, OutletId outletId, CancellationToken cancellationToken = default);
    Task AddAsync(Terminal terminal, CancellationToken cancellationToken = default);
    Task UpdateAsync(Terminal terminal, CancellationToken cancellationToken = default);
}

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken = default);
    Task<Device?> GetByCodeAsync(TenantId tenantId, string deviceCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(Device device, CancellationToken cancellationToken = default);
    Task UpdateAsync(Device device, CancellationToken cancellationToken = default);
}
