using Yashdeep.Domain.Common;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational.Events;

public sealed record TenantCreatedEvent(
    TenantId TenantId,
    string LegalName,
    string TradeName,
    TenantStatus Status,
    DateTime OccurredOnUtc
) : IDomainEvent;

public sealed record TenantStatusChangedEvent(
    TenantId TenantId,
    TenantStatus PreviousStatus,
    TenantStatus NewStatus,
    DateTime OccurredOnUtc
) : IDomainEvent;
