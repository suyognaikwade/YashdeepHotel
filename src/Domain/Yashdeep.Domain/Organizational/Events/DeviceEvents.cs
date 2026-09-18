using Yashdeep.Domain.Common;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational.Events;

public sealed record DeviceRegisteredEvent(
    DeviceId DeviceId,
    TenantId TenantId,
    string DeviceCode,
    string HardwareFingerprint,
    DeviceScope Scope,
    DeviceStatus Status,
    DateTime OccurredOnUtc
) : IDomainEvent;

public sealed record DeviceStatusChangedEvent(
    DeviceId DeviceId,
    TenantId TenantId,
    DeviceStatus PreviousStatus,
    DeviceStatus NewStatus,
    DateTime OccurredOnUtc
) : IDomainEvent;

public sealed record DeviceAssignedToScopeEvent(
    DeviceId DeviceId,
    TenantId TenantId,
    DeviceScope Scope,
    BranchId? BranchId,
    OutletId? OutletId,
    TerminalId? TerminalId,
    DateTime OccurredOnUtc
) : IDomainEvent;
