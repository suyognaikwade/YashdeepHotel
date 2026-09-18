using Yashdeep.Domain.Common;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational.Events;

public sealed record TerminalCreatedEvent(
    TerminalId TerminalId,
    OutletId OutletId,
    TenantId TenantId,
    string TerminalCode,
    string Name,
    TerminalStatus Status,
    DateTime OccurredOnUtc
) : IDomainEvent;

public sealed record TerminalStatusChangedEvent(
    TerminalId TerminalId,
    TenantId TenantId,
    TerminalStatus PreviousStatus,
    TerminalStatus NewStatus,
    DateTime OccurredOnUtc
) : IDomainEvent;
