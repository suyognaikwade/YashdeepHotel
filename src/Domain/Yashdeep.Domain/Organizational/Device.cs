using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.Organizational.Events;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Organizational;

public sealed class Device : AggregateRoot<DeviceId>
{
    public TenantId TenantId { get; }
    public string DeviceCode { get; private set; }
    public string HardwareFingerprint { get; private set; }
    public string DeviceName { get; private set; }
    public DeviceScope Scope { get; private set; }
    public BranchId? BranchId { get; private set; }
    public OutletId? OutletId { get; private set; }
    public TerminalId? TerminalId { get; private set; }
    public DeviceStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Device(
        DeviceId id,
        TenantId tenantId,
        string deviceCode,
        string hardwareFingerprint,
        string deviceName,
        DeviceScope scope,
        BranchId? branchId,
        OutletId? outletId,
        TerminalId? terminalId,
        DeviceStatus status,
        DateTime createdAtUtc)
        : base(id)
    {
        TenantId = tenantId ?? throw new InvalidOwnershipException("Device must belong to a valid tenant.");
        DeviceCode = deviceCode;
        HardwareFingerprint = hardwareFingerprint;
        DeviceName = deviceName;
        Scope = scope;
        BranchId = branchId;
        OutletId = outletId;
        TerminalId = terminalId;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public static Device Register(
        TenantId tenantId,
        string deviceCode,
        string hardwareFingerprint,
        string deviceName,
        DeviceScope scope = DeviceScope.TenantLevel,
        BranchId? branchId = null,
        OutletId? outletId = null,
        TerminalId? terminalId = null,
        DeviceId? id = null,
        DeviceStatus initialStatus = DeviceStatus.PendingProvisioning)
    {
        if (tenantId is null)
        {
            throw new InvalidOwnershipException("TenantId is required to register a Device.");
        }

        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            throw new DomainException("Device code is required.");
        }

        if (string.IsNullOrWhiteSpace(hardwareFingerprint))
        {
            throw new DomainException("Hardware fingerprint is required.");
        }

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new DomainException("Device name is required.");
        }

        ValidateScopeHierarchy(scope, branchId, outletId, terminalId);

        var deviceId = id ?? DeviceId.New();
        var now = DateTime.UtcNow;

        var device = new Device(
            deviceId,
            tenantId,
            deviceCode.Trim().ToUpperInvariant(),
            hardwareFingerprint.Trim(),
            deviceName.Trim(),
            scope,
            branchId,
            outletId,
            terminalId,
            initialStatus,
            now);

        device.AddDomainEvent(new DeviceRegisteredEvent(
            device.Id,
            device.TenantId,
            device.DeviceCode,
            device.HardwareFingerprint,
            device.Scope,
            device.Status,
            now));

        if (branchId is not null || outletId is not null || terminalId is not null)
        {
            device.AddDomainEvent(new DeviceAssignedToScopeEvent(
                device.Id,
                device.TenantId,
                device.Scope,
                device.BranchId,
                device.OutletId,
                device.TerminalId,
                now));
        }

        return device;
    }

    public void AssignScope(
        DeviceScope scope,
        BranchId? branchId,
        OutletId? outletId,
        TerminalId? terminalId)
    {
        if (Status == DeviceStatus.Revoked)
        {
            throw new InvalidStateTransitionException("Cannot assign operational scope to a revoked device.");
        }

        ValidateScopeHierarchy(scope, branchId, outletId, terminalId);

        Scope = scope;
        BranchId = branchId;
        OutletId = outletId;
        TerminalId = terminalId;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new DeviceAssignedToScopeEvent(
            Id,
            TenantId,
            Scope,
            BranchId,
            OutletId,
            TerminalId,
            UpdatedAtUtc));
    }

    public void AssignToTerminal(Terminal terminal)
    {
        if (terminal is null)
        {
            throw new ArgumentNullException(nameof(terminal));
        }

        if (terminal.TenantId != TenantId)
        {
            throw new InvalidOwnershipException($"Cross-tenant assignment blocked: Terminal tenant '{terminal.TenantId}' does not match Device tenant '{TenantId}'.");
        }

        AssignScope(
            DeviceScope.TerminalLevel,
            branchId: null,
            outletId: terminal.OutletId,
            terminalId: terminal.Id);
    }

    public void ChangeStatus(DeviceStatus newStatus)
    {
        if (Status == DeviceStatus.Revoked)
        {
            throw new InvalidStateTransitionException("Cannot change status of a revoked device.");
        }

        if (Status == newStatus)
        {
            return;
        }

        var previous = Status;
        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new DeviceStatusChangedEvent(
            Id,
            TenantId,
            previous,
            newStatus,
            UpdatedAtUtc));
    }

    public void Activate() => ChangeStatus(DeviceStatus.Active);
    public void Suspend() => ChangeStatus(DeviceStatus.Suspended);
    public void Revoke() => ChangeStatus(DeviceStatus.Revoked);

    private static void ValidateScopeHierarchy(
        DeviceScope scope,
        BranchId? branchId,
        OutletId? outletId,
        TerminalId? terminalId)
    {
        switch (scope)
        {
            case DeviceScope.TenantLevel:
                if (branchId is not null || outletId is not null || terminalId is not null)
                {
                    throw new DomainException("Tenant-level device scope must not specify BranchId, OutletId, or TerminalId.");
                }
                break;

            case DeviceScope.BranchLevel:
                if (branchId is null)
                {
                    throw new DomainException("Branch-level device scope requires BranchId.");
                }
                if (outletId is not null || terminalId is not null)
                {
                    throw new DomainException("Branch-level device scope must not specify OutletId or TerminalId.");
                }
                break;

            case DeviceScope.OutletLevel:
                if (outletId is null)
                {
                    throw new DomainException("Outlet-level device scope requires OutletId.");
                }
                if (terminalId is not null)
                {
                    throw new DomainException("Outlet-level device scope must not specify TerminalId.");
                }
                break;

            case DeviceScope.TerminalLevel:
                if (terminalId is null)
                {
                    throw new DomainException("Terminal-level device scope requires TerminalId.");
                }
                if (outletId is null)
                {
                    throw new DomainException("Terminal-level device scope requires parent OutletId.");
                }
                break;

            default:
                throw new DomainException($"Unsupported device scope: {scope}.");
        }
    }
}
