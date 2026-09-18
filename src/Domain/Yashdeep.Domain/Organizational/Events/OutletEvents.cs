using Yashdeep.Domain.Common;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational.Events;

public sealed record OutletCreatedEvent(
    OutletId OutletId,
    BranchId BranchId,
    TenantId TenantId,
    string Name,
    OutletType OutletType,
    OutletStatus Status,
    DateTime OccurredOnUtc
) : IDomainEvent;

public sealed record OutletStatusChangedEvent(
    OutletId OutletId,
    TenantId TenantId,
    OutletStatus PreviousStatus,
    OutletStatus NewStatus,
    DateTime OccurredOnUtc
) : IDomainEvent;
