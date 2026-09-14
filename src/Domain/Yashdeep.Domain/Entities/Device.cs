using System;
using System.Collections.Generic;
using Yashdeep.Domain.Enums;
using Yashdeep.Domain.Events;

namespace Yashdeep.Domain.Entities;

public class Device
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid? OutletId { get; private set; }
    public Guid? TerminalId { get; private set; }

    public string DeviceName { get; private set; } = string.Empty;
    public string HardwareFingerprint { get; private set; } = string.Empty;
    public string Platform { get; private set; } = string.Empty;
    public DeviceType Type { get; private set; }
    public DeviceLifecycleState State { get; private set; }

    public string ActivationCode { get; private set; } = string.Empty;
    public DateTime? ActivationCodeExpiresUtc { get; private set; }
    public string? CertificateThumbprint { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastUpdatedUtc { get; private set; }
    public DateTime? LastActiveAtUtc { get; private set; }

    public string? StatusReason { get; private set; }
    public Guid? ReplacedByDeviceId { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Device() { }

    public static Device CreatePending(
        Guid id,
        Guid tenantId,
        Guid organizationId,
        Guid branchId,
        Guid? outletId,
        Guid? terminalId,
        string deviceName,
        string hardwareFingerprint,
        string platform,
        DeviceType type,
        string activationCode,
        DateTime activationCodeExpiresUtc,
        string performedBy = "System")
    {
        if (id == Guid.Empty) throw new ArgumentException("Device Id cannot be empty.", nameof(id));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (organizationId == Guid.Empty) throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId cannot be empty.", nameof(branchId));
        if (string.IsNullOrWhiteSpace(hardwareFingerprint)) throw new ArgumentException("Hardware fingerprint cannot be empty.", nameof(hardwareFingerprint));
        if (string.IsNullOrWhiteSpace(activationCode)) throw new ArgumentException("Activation code cannot be empty.", nameof(activationCode));

        var now = DateTime.UtcNow;
        var device = new Device
        {
            Id = id,
            TenantId = tenantId,
            OrganizationId = organizationId,
            BranchId = branchId,
            OutletId = outletId,
            TerminalId = terminalId,
            DeviceName = deviceName,
            HardwareFingerprint = hardwareFingerprint,
            Platform = platform,
            Type = type,
            State = DeviceLifecycleState.Pending,
            ActivationCode = activationCode,
            ActivationCodeExpiresUtc = activationCodeExpiresUtc,
            CreatedAtUtc = now,
            LastUpdatedUtc = now
        };

        device.AddDomainEvent(new DeviceRegisteredEvent(
            Guid.NewGuid(),
            device.Id,
            device.TenantId,
            device.BranchId,
            device.HardwareFingerprint,
            now,
            performedBy,
            $"Device registered in Pending state with Activation Code. Type: {type}, Platform: {platform}"));

        return device;
    }

    public void Activate(string codeProvided, string? certificateThumbprint = null, string performedBy = "System")
    {
        if (State == DeviceLifecycleState.Revoked)
        {
            throw new InvalidOperationException($"Cannot activate device {Id} because it is Revoked.");
        }
        if (State == DeviceLifecycleState.Suspended)
        {
            throw new InvalidOperationException($"Cannot activate device {Id} directly from Suspended state. Use Recover first.");
        }
        if (State == DeviceLifecycleState.Retired)
        {
            throw new InvalidOperationException($"Cannot activate device {Id} because it is Retired.");
        }
        if (State == DeviceLifecycleState.Active)
        {
            // Already active, refresh timestamp
            LastUpdatedUtc = DateTime.UtcNow;
            return;
        }

        if (ActivationCodeExpiresUtc.HasValue && DateTime.UtcNow > ActivationCodeExpiresUtc.Value)
        {
            throw new InvalidOperationException($"Activation code for device {Id} has expired.");
        }

        if (!string.Equals(ActivationCode, codeProvided, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Invalid activation code for device {Id}.");
        }

        var now = DateTime.UtcNow;
        State = DeviceLifecycleState.Active;
        CertificateThumbprint = certificateThumbprint;
        LastUpdatedUtc = now;
        LastActiveAtUtc = now;
        StatusReason = "Activated successfully";

        AddDomainEvent(new DeviceActivatedEvent(
            Guid.NewGuid(),
            Id,
            TenantId,
            BranchId,
            HardwareFingerprint,
            now,
            performedBy,
            "Device successfully activated"));
    }

    public void Suspend(string reason, string performedBy = "Admin")
    {
        if (State == DeviceLifecycleState.Revoked)
        {
            throw new InvalidOperationException($"Cannot suspend device {Id} because it is already Revoked.");
        }
        if (State == DeviceLifecycleState.Retired)
        {
            throw new InvalidOperationException($"Cannot suspend device {Id} because it is Retired.");
        }

        var now = DateTime.UtcNow;
        State = DeviceLifecycleState.Suspended;
        StatusReason = reason;
        LastUpdatedUtc = now;

        AddDomainEvent(new DeviceSuspendedEvent(
            Guid.NewGuid(),
            Id,
            TenantId,
            BranchId,
            HardwareFingerprint,
            now,
            performedBy,
            $"Device suspended. Reason: {reason}"));
    }

    public void Revoke(string reason, string performedBy = "Admin")
    {
        if (State == DeviceLifecycleState.Retired)
        {
            throw new InvalidOperationException($"Cannot revoke device {Id} because it is already Retired.");
        }

        var now = DateTime.UtcNow;
        State = DeviceLifecycleState.Revoked;
        StatusReason = reason;
        LastUpdatedUtc = now;

        AddDomainEvent(new DeviceRevokedEvent(
            Guid.NewGuid(),
            Id,
            TenantId,
            BranchId,
            HardwareFingerprint,
            now,
            performedBy,
            $"Device permanently revoked. Reason: {reason}"));
    }

    public void Recover(string reason, string performedBy = "Admin")
    {
        if (State != DeviceLifecycleState.Suspended)
        {
            throw new InvalidOperationException($"Only Suspended devices can be recovered. Current state: {State}");
        }

        var now = DateTime.UtcNow;
        State = DeviceLifecycleState.Active;
        StatusReason = $"Recovered from suspension: {reason}";
        LastUpdatedUtc = now;
        LastActiveAtUtc = now;

        AddDomainEvent(new DeviceRecoveredEvent(
            Guid.NewGuid(),
            Id,
            TenantId,
            BranchId,
            HardwareFingerprint,
            now,
            performedBy,
            $"Device recovered. Reason: {reason}"));
    }

    public Device Replace(
        Guid replacementDeviceId,
        string newHardwareFingerprint,
        string newDeviceName,
        string newActivationCode,
        DateTime newActivationCodeExpiresUtc,
        string reason,
        string performedBy = "Admin")
    {
        if (State == DeviceLifecycleState.Retired)
        {
            throw new InvalidOperationException($"Device {Id} is already Retired.");
        }

        var now = DateTime.UtcNow;
        ReplacedByDeviceId = replacementDeviceId;
        State = DeviceLifecycleState.Retired;
        StatusReason = $"Replaced by device {replacementDeviceId}. Reason: {reason}";
        LastUpdatedUtc = now;

        AddDomainEvent(new DeviceReplacedEvent(
            Guid.NewGuid(),
            Id,
            replacementDeviceId,
            TenantId,
            BranchId,
            HardwareFingerprint,
            now,
            performedBy,
            $"Device replaced by {replacementDeviceId}. Reason: {reason}"));

        var newDevice = CreatePending(
            replacementDeviceId,
            TenantId,
            OrganizationId,
            BranchId,
            OutletId,
            TerminalId,
            newDeviceName,
            newHardwareFingerprint,
            Platform,
            Type,
            newActivationCode,
            newActivationCodeExpiresUtc,
            performedBy);

        return newDevice;
    }

    public void Retire(string reason, string performedBy = "Admin")
    {
        var now = DateTime.UtcNow;
        State = DeviceLifecycleState.Retired;
        StatusReason = reason;
        LastUpdatedUtc = now;
    }

    public void RecordActivity()
    {
        if (State != DeviceLifecycleState.Active)
        {
            throw new InvalidOperationException($"Cannot record activity for device {Id} in state {State}");
        }

        var now = DateTime.UtcNow;
        LastActiveAtUtc = now;
        LastUpdatedUtc = now;
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    private void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}
