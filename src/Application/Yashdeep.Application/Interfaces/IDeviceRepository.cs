using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Application.Interfaces;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id);
    Task<Device?> GetByHardwareFingerprintAsync(Guid tenantId, string hardwareFingerprint);
    Task<IEnumerable<Device>> GetActiveDevicesForBranchAsync(Guid tenantId, Guid branchId);
    Task<IEnumerable<Device>> GetAllForTenantAsync(Guid tenantId);
    Task AddAsync(Device device);
    Task UpdateAsync(Device device);
}
