using Yashdeep.Domain.Common;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational.Events;

public sealed record BranchCreatedEvent(
    BranchId BranchId,
    OrganizationId OrganizationId,
    TenantId TenantId,
    string Name,
    string BranchCode,
    BranchStatus Status,
    DateTime OccurredOnUtc
) : IDomainEvent;

public sealed record BranchStatusChangedEvent(
    BranchId BranchId,
    TenantId TenantId,
    BranchStatus PreviousStatus,
    BranchStatus NewStatus,
    DateTime OccurredOnUtc
) : IDomainEvent;
