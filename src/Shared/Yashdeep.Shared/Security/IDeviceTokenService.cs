using System;

namespace Yashdeep.Shared.Security;

public record DeviceTokenPayload(
    Guid DeviceId,
    Guid TenantId,
    Guid OrganizationId,
    Guid BranchId,
    Guid? OutletId,
    Guid? TerminalId,
    string HardwareFingerprint,
    string LifecycleState,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);

public record DeviceTokenResult(
    bool IsValid,
    string Token,
    DeviceTokenPayload? Payload,
    string? ErrorReason);

public interface IDeviceTokenService
{
    string IssueToken(DeviceTokenPayload payload);
    DeviceTokenResult ValidateToken(string token);
}
