using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yashdeep.Application.Interfaces;
using Yashdeep.Domain.Entities;
using Yashdeep.Domain.Enums;

namespace Yashdeep.Infrastructure.Persistence;

public class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly ConcurrentDictionary<Guid, Device> _devices = new();

    public Task<Device?> GetByIdAsync(Guid id)
    {
        _devices.TryGetValue(id, out var device);
        return Task.FromResult(device);
    }

    public Task<Device?> GetByHardwareFingerprintAsync(Guid tenantId, string hardwareFingerprint)
    {
        var device = _devices.Values.FirstOrDefault(d =>
            d.TenantId == tenantId &&
            string.Equals(d.HardwareFingerprint, hardwareFingerprint, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(device);
    }

    public Task<IEnumerable<Device>> GetActiveDevicesForBranchAsync(Guid tenantId, Guid branchId)
    {
        var activeDevices = _devices.Values.Where(d =>
            d.TenantId == tenantId &&
            d.BranchId == branchId &&
            (d.State == DeviceLifecycleState.Active || d.State == DeviceLifecycleState.Pending));
        return Task.FromResult<IEnumerable<Device>>(activeDevices.ToList());
    }

    public Task<IEnumerable<Device>> GetAllForTenantAsync(Guid tenantId)
    {
        var tenantDevices = _devices.Values.Where(d => d.TenantId == tenantId);
        return Task.FromResult<IEnumerable<Device>>(tenantDevices.ToList());
    }

    public Task AddAsync(Device device)
    {
        _devices[device.Id] = device;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Device device)
    {
        _devices[device.Id] = device;
        return Task.CompletedTask;
    }
}
