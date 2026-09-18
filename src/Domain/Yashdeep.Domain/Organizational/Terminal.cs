using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.Organizational.Events;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational;

public sealed class Terminal : AggregateRoot<TerminalId>
{
    public TenantId TenantId { get; }
    public OutletId OutletId { get; }
    public string TerminalCode { get; private set; }
    public string Name { get; private set; }
    public TerminalStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Terminal(
        TerminalId id,
        TenantId tenantId,
        OutletId outletId,
        string terminalCode,
        string name,
        TerminalStatus status,
        DateTime createdAtUtc)
        : base(id)
    {
        TenantId = tenantId ?? throw new InvalidOwnershipException("Terminal must belong to a valid tenant.");
        OutletId = outletId ?? throw new InvalidOwnershipException("Terminal must belong to a valid outlet.");
        TerminalCode = terminalCode;
        Name = name;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public static Terminal Create(
        Outlet outlet,
        string terminalCode,
        string name,
        TerminalId? id = null,
        TerminalStatus initialStatus = TerminalStatus.Active)
    {
        if (outlet is null)
        {
            throw new ArgumentNullException(nameof(outlet));
        }

        return Create(
            outlet.TenantId,
            outlet.Id,
            terminalCode,
            name,
            id,
            initialStatus);
    }

    public static Terminal Create(
        TenantId tenantId,
        OutletId outletId,
        string terminalCode,
        string name,
        TerminalId? id = null,
        TerminalStatus initialStatus = TerminalStatus.Active)
    {
        if (tenantId is null)
        {
            throw new InvalidOwnershipException("TenantId is required to create a Terminal.");
        }

        if (outletId is null)
        {
            throw new InvalidOwnershipException("OutletId is required to create a Terminal.");
        }

        if (string.IsNullOrWhiteSpace(terminalCode))
        {
            throw new DomainException("Terminal code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Terminal name is required.");
        }

        var terminalId = id ?? TerminalId.New();
        var now = DateTime.UtcNow;

        var terminal = new Terminal(
            terminalId,
            tenantId,
            outletId,
            terminalCode.Trim().ToUpperInvariant(),
            name.Trim(),
            initialStatus,
            now);

        terminal.AddDomainEvent(new TerminalCreatedEvent(
            terminal.Id,
            terminal.OutletId,
            terminal.TenantId,
            terminal.TerminalCode,
            terminal.Name,
            terminal.Status,
            now));

        return terminal;
    }

    public void ChangeStatus(TerminalStatus newStatus)
    {
        if (Status == newStatus)
        {
            return;
        }

        var previous = Status;
        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new TerminalStatusChangedEvent(
            Id,
            TenantId,
            previous,
            newStatus,
            UpdatedAtUtc));
    }

    public void Activate() => ChangeStatus(TerminalStatus.Active);
    public void Deactivate() => ChangeStatus(TerminalStatus.Inactive);
    public void SetMaintenance() => ChangeStatus(TerminalStatus.Maintenance);
}
