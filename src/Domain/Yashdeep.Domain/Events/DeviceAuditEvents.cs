using Yashdeep.Shared.Primitives;

namespace Yashdeep.Domain.Events;

public abstract record DeviceAuditEvent(
    Guid EventId,
    Guid DeviceId,
    Guid TenantId,
    Guid BranchId,
    string HardwareFingerprint,
    DateTime OccurredOnUtc,
    string PerformedBy,
    string Details) : IDomainEvent
{
    public DateTime OccurredUtc => OccurredOnUtc;
}

public record DeviceRegisteredEvent(
    Guid EventId,
    Guid DeviceId,
    Guid TenantId,
    Guid BranchId,
    string HardwareFingerprint,
    DateTime OccurredOnUtc,
    string PerformedBy,
    string Details) : DeviceAuditEvent(EventId, DeviceId, TenantId, BranchId, HardwareFingerprint, OccurredOnUtc, PerformedBy, Details);

public record DeviceActivatedEvent(
    Guid EventId,
    Guid DeviceId,
    Guid TenantId,
    Guid BranchId,
    string HardwareFingerprint,
    DateTime OccurredOnUtc,
    string PerformedBy,
    string Details) : DeviceAuditEvent(EventId, DeviceId, TenantId, BranchId, HardwareFingerprint, OccurredOnUtc, PerformedBy, Details);

public record DeviceSuspendedEvent(
    Guid EventId,
    Guid DeviceId,
    Guid TenantId,
    Guid BranchId,
    string HardwareFingerprint,
    DateTime OccurredOnUtc,
    string PerformedBy,
    string Details) : DeviceAuditEvent(EventId, DeviceId, TenantId, BranchId, HardwareFingerprint, OccurredOnUtc, PerformedBy, Details);

public record DeviceRevokedEvent(
    Guid EventId,
    Guid DeviceId,
    Guid TenantId,
    Guid BranchId,
    string HardwareFingerprint,
    DateTime OccurredOnUtc,
    string PerformedBy,
    string Details) : DeviceAuditEvent(EventId, DeviceId, TenantId, BranchId, HardwareFingerprint, OccurredOnUtc, PerformedBy, Details);

public record DeviceReplacedEvent(
    Guid EventId,
    Guid DeviceId,
    Guid ReplacementDeviceId,
    Guid TenantId,
    Guid BranchId,
    string HardwareFingerprint,
    DateTime OccurredOnUtc,
    string PerformedBy,
    string Details) : DeviceAuditEvent(EventId, DeviceId, TenantId, BranchId, HardwareFingerprint, OccurredOnUtc, PerformedBy, Details);

public record DeviceRecoveredEvent(
    Guid EventId,
    Guid DeviceId,
    Guid TenantId,
    Guid BranchId,
    string HardwareFingerprint,
    DateTime OccurredOnUtc,
    string PerformedBy,
    string Details) : DeviceAuditEvent(EventId, DeviceId, TenantId, BranchId, HardwareFingerprint, OccurredOnUtc, PerformedBy, Details);
