using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yashdeep.Application.Models;

namespace Yashdeep.Application.Services;

public interface IDeviceRegistrationService
{
    Task<RegisterDeviceResponse> RegisterDeviceAsync(RegisterDeviceRequest request);
    Task<ActivateDeviceResponse> ActivateDeviceAsync(ActivateDeviceRequest request);
    Task SuspendDeviceAsync(SuspendDeviceRequest request);
    Task RevokeDeviceAsync(RevokeDeviceRequest request);
    Task RecoverDeviceAsync(RecoverDeviceRequest request);
    Task<RegisterDeviceResponse> ReplaceDeviceAsync(ReplaceDeviceRequest request);
    Task<DeviceStatusResponse> GetDeviceStatusAsync(Guid requestTenantId, Guid deviceId);
    Task<IEnumerable<DeviceStatusResponse>> GetBranchDevicesAsync(Guid requestTenantId, Guid branchId);
}
