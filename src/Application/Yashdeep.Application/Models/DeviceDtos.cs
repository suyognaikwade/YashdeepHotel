using System;
using Yashdeep.Domain.Enums;

namespace Yashdeep.Application.Models;

public record RegisterDeviceRequest(
    Guid TenantId,
    Guid OrganizationId,
    Guid BranchId,
    Guid? OutletId,
    Guid? TerminalId,
    string DeviceName,
    string HardwareFingerprint,
    string Platform,
    DeviceType Type,
    string PerformedBy = "Admin");

public record RegisterDeviceResponse(
    Guid DeviceId,
    Guid TenantId,
    Guid BranchId,
    string ActivationCode,
    DateTime ActivationCodeExpiresUtc,
    string Status);

public record ActivateDeviceRequest(
    Guid TenantId,
    Guid BranchId,
    Guid DeviceId,
    string ActivationCode,
    string HardwareFingerprint,
    string? CertificateThumbprint = null,
    string PerformedBy = "DeviceClient");

public record ActivateDeviceResponse(
    Guid DeviceId,
    Guid TenantId,
    Guid BranchId,
    string Status,
    string DeviceToken,
    DateTime TokenExpiresUtc);

public record SuspendDeviceRequest(
    Guid TenantId,
    Guid DeviceId,
    string Reason,
    string PerformedBy = "Admin");

public record RevokeDeviceRequest(
    Guid TenantId,
    Guid DeviceId,
    string Reason,
    string PerformedBy = "Admin");

public record RecoverDeviceRequest(
    Guid TenantId,
    Guid DeviceId,
    string Reason,
    string PerformedBy = "Admin");

public record ReplaceDeviceRequest(
    Guid TenantId,
    Guid ExistingDeviceId,
    string NewHardwareFingerprint,
    string NewDeviceName,
    string Reason,
    string PerformedBy = "Admin");

public record DeviceStatusResponse(
    Guid DeviceId,
    Guid TenantId,
    Guid OrganizationId,
    Guid BranchId,
    Guid? OutletId,
    Guid? TerminalId,
    string DeviceName,
    string HardwareFingerprint,
    string Platform,
    DeviceType Type,
    DeviceLifecycleState State,
    DateTime CreatedAtUtc,
    DateTime? LastActiveAtUtc,
    string? StatusReason,
    Guid? ReplacedByDeviceId);
