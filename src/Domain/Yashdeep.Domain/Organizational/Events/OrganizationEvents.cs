using Yashdeep.Domain.Common;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational.Events;

public sealed record OrganizationCreatedEvent(
    OrganizationId OrganizationId,
    TenantId TenantId,
    string LegalName,
    OrganizationStatus Status,
    DateTime OccurredOnUtc
) : IDomainEvent;

public sealed record OrganizationStatusChangedEvent(
    OrganizationId OrganizationId,
    TenantId TenantId,
    OrganizationStatus PreviousStatus,
    OrganizationStatus NewStatus,
    DateTime OccurredOnUtc
) : IDomainEvent;
