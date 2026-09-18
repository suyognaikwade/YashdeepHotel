using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Yashdeep.Application.Interfaces;
using Yashdeep.Application.Models;
using Yashdeep.Domain.Entities;
using Yashdeep.Domain.Enums;
using Yashdeep.Shared.Security;

namespace Yashdeep.Application.Services;

public class DeviceRegistrationService : IDeviceRegistrationService
{
    private readonly IDeviceRepository _repository;
    private readonly IDeviceTokenService _tokenService;
    private readonly IAuditEventLogger _auditLogger;

    public DeviceRegistrationService(
        IDeviceRepository repository,
        IDeviceTokenService tokenService,
        IAuditEventLogger auditLogger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<RegisterDeviceResponse> RegisterDeviceAsync(RegisterDeviceRequest request)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (request.OrganizationId == Guid.Empty) throw new ArgumentException("OrganizationId is required.", nameof(request));
        if (request.BranchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.HardwareFingerprint)) throw new ArgumentException("HardwareFingerprint is required.", nameof(request));

        // Duplicate device registration check
        var existing = await _repository.GetByHardwareFingerprintAsync(request.TenantId, request.HardwareFingerprint);
        if (existing != null && existing.State != DeviceLifecycleState.Retired)
        {
            throw new InvalidOperationException($"Duplicate device registration attempt. Hardware fingerprint '{request.HardwareFingerprint}' is already registered in state '{existing.State}'.");
        }

        var deviceId = Guid.NewGuid();
        var activationCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var expiresUtc = DateTime.UtcNow.AddHours(24);

        var device = Device.CreatePending(
            deviceId,
            request.TenantId,
            request.OrganizationId,
            request.BranchId,
            request.OutletId,
            request.TerminalId,
            request.DeviceName,
            request.HardwareFingerprint,
            request.Platform,
            request.Type,
            activationCode,
            expiresUtc,
            request.PerformedBy);

        await _repository.AddAsync(device);
        await _auditLogger.LogEventsAsync(device.DomainEvents);
        device.ClearDomainEvents();

        return new RegisterDeviceResponse(
            device.Id,
            device.TenantId,
            device.BranchId,
            activationCode,
            expiresUtc,
            device.State.ToString());
    }

    public async Task<ActivateDeviceResponse> ActivateDeviceAsync(ActivateDeviceRequest request)
    {
        var device = await _repository.GetByIdAsync(request.DeviceId);
        if (device == null)
        {
            throw new KeyNotFoundException($"Device {request.DeviceId} not found.");
        }

        // Cross-tenant access check
        if (device.TenantId != request.TenantId)
        {
            throw new UnauthorizedAccessException($"Cross-tenant device access denied for tenant {request.TenantId}.");
        }

        // Branch-bound check
        if (device.BranchId != request.BranchId)
        {
            throw new InvalidOperationException($"Branch mismatch. Device belongs to branch {device.BranchId}, but activation requested for branch {request.BranchId}.");
        }

        // Lifecycle checks
        if (device.State == DeviceLifecycleState.Suspended)
        {
            throw new InvalidOperationException($"Suspended device rejection: Cannot activate suspended device {device.Id}. Recover the device first.");
        }
        if (device.State == DeviceLifecycleState.Revoked)
        {
            throw new InvalidOperationException($"Revoked device rejection: Cannot activate revoked device {device.Id}.");
        }

        // Verify hardware fingerprint matches
        if (!string.Equals(device.HardwareFingerprint, request.HardwareFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Hardware fingerprint mismatch during activation.");
        }

        try
        {
            device.Activate(request.ActivationCode, request.CertificateThumbprint, request.PerformedBy);
        }
        catch (InvalidOperationException ex)
        {
            throw new UnauthorizedAccessException($"Unauthorized device activation: {ex.Message}", ex);
        }

        await _repository.UpdateAsync(device);
        await _auditLogger.LogEventsAsync(device.DomainEvents);
        device.ClearDomainEvents();

        var tokenExpires = DateTime.UtcNow.AddDays(7); // 7-day token issuance foundation
        var payload = new DeviceTokenPayload(
            device.Id,
            device.TenantId,
            device.OrganizationId,
            device.BranchId,
            device.OutletId,
            device.TerminalId,
            device.HardwareFingerprint,
            device.State.ToString(),
            DateTime.UtcNow,
            tokenExpires);

        var token = _tokenService.IssueToken(payload);

        return new ActivateDeviceResponse(
            device.Id,
            device.TenantId,
            device.BranchId,
            device.State.ToString(),
            token,
            tokenExpires);
    }

    public async Task SuspendDeviceAsync(SuspendDeviceRequest request)
    {
        var device = await GetTenantDeviceOrThrow(request.TenantId, request.DeviceId);
        device.Suspend(request.Reason, request.PerformedBy);
        await _repository.UpdateAsync(device);
        await _auditLogger.LogEventsAsync(device.DomainEvents);
        device.ClearDomainEvents();
    }

    public async Task RevokeDeviceAsync(RevokeDeviceRequest request)
    {
        var device = await GetTenantDeviceOrThrow(request.TenantId, request.DeviceId);
        device.Revoke(request.Reason, request.PerformedBy);
        await _repository.UpdateAsync(device);
        await _auditLogger.LogEventsAsync(device.DomainEvents);
        device.ClearDomainEvents();
    }

    public async Task RecoverDeviceAsync(RecoverDeviceRequest request)
    {
        var device = await GetTenantDeviceOrThrow(request.TenantId, request.DeviceId);
        device.Recover(request.Reason, request.PerformedBy);
        await _repository.UpdateAsync(device);
        await _auditLogger.LogEventsAsync(device.DomainEvents);
        device.ClearDomainEvents();
    }

    public async Task<RegisterDeviceResponse> ReplaceDeviceAsync(ReplaceDeviceRequest request)
    {
        var existingDevice = await GetTenantDeviceOrThrow(request.TenantId, request.ExistingDeviceId);

        var newDeviceId = Guid.NewGuid();
        var newActivationCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var expiresUtc = DateTime.UtcNow.AddHours(24);

        var newDevice = existingDevice.Replace(
            newDeviceId,
            request.NewHardwareFingerprint,
            request.NewDeviceName,
            newActivationCode,
            expiresUtc,
            request.Reason,
            request.PerformedBy);

        await _repository.UpdateAsync(existingDevice);
        await _auditLogger.LogEventsAsync(existingDevice.DomainEvents);
        existingDevice.ClearDomainEvents();

        await _repository.AddAsync(newDevice);
        await _auditLogger.LogEventsAsync(newDevice.DomainEvents);
        newDevice.ClearDomainEvents();

        return new RegisterDeviceResponse(
            newDevice.Id,
            newDevice.TenantId,
            newDevice.BranchId,
            newActivationCode,
            expiresUtc,
            newDevice.State.ToString());
    }

    public async Task<DeviceStatusResponse> GetDeviceStatusAsync(Guid requestTenantId, Guid deviceId)
    {
        var device = await GetTenantDeviceOrThrow(requestTenantId, deviceId);
        return MapToResponse(device);
    }

    public async Task<IEnumerable<DeviceStatusResponse>> GetBranchDevicesAsync(Guid requestTenantId, Guid branchId)
    {
        var devices = await _repository.GetActiveDevicesForBranchAsync(requestTenantId, branchId);
        return devices.Select(MapToResponse);
    }

    private async Task<Device> GetTenantDeviceOrThrow(Guid tenantId, Guid deviceId)
    {
        var device = await _repository.GetByIdAsync(deviceId);
        if (device == null)
        {
            throw new KeyNotFoundException($"Device {deviceId} not found.");
        }
        if (device.TenantId != tenantId)
        {
            throw new UnauthorizedAccessException($"Cross-tenant device access denied for tenant {tenantId}.");
        }
        return device;
    }

    private static DeviceStatusResponse MapToResponse(Device device)
    {
        return new DeviceStatusResponse(
            device.Id,
            device.TenantId,
            device.OrganizationId,
            device.BranchId,
            device.OutletId,
            device.TerminalId,
            device.DeviceName,
            device.HardwareFingerprint,
            device.Platform,
            device.Type,
            device.State,
            device.CreatedAtUtc,
            device.LastActiveAtUtc,
            device.StatusReason,
            device.ReplacedByDeviceId);
    }
}
