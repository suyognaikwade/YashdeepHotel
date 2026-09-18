using System;
using System.Collections.Generic;
using Yashdeep.Domain.Enums;
using Yashdeep.Domain.Events;
using Yashdeep.Shared.Primitives;

namespace Yashdeep.Domain.Entities;

/// <summary>
/// Hardware device instance aggregate tracking registered edge hardware and lifecycle state.
/// Harmonized to support both rich domain lifecycle workflows and local persistence storage.
/// </summary>
public class Device
{
    private readonly List<IDomainEvent> _domainEvents = new();
    private string _hardwareId = string.Empty;

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? OutletId { get; set; }
    public Guid? TerminalId { get; set; }

    public string DeviceName { get; set; } = string.Empty;
    public string HardwareFingerprint { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DeviceType Type { get; set; }
    public DeviceLifecycleState State { get; set; }

    public string ActivationCode { get; set; } = string.Empty;
    public DateTime? ActivationCodeExpiresUtc { get; set; }
    public string? CertificateThumbprint { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastActiveAtUtc { get; set; }

    public string? StatusReason { get; set; }
    public Guid? ReplacedByDeviceId { get; set; }

    // Compatibility properties for Local Persistence (PR #37)
    public string HardwareId
    {
        get => string.IsNullOrEmpty(_hardwareId) ? HardwareFingerprint : _hardwareId;
        set
        {
            _hardwareId = value;
            if (string.IsNullOrEmpty(HardwareFingerprint))
            {
                HardwareFingerprint = value;
            }
        }
    }

    public string DeviceCertificate
    {
        get => CertificateThumbprint ?? string.Empty;
        set => CertificateThumbprint = value;
    }

    public bool IsRegistered
    {
        get => State == DeviceLifecycleState.Active;
        set
        {
            if (value && State != DeviceLifecycleState.Active)
            {
                State = DeviceLifecycleState.Active;
            }
            else if (!value && State == DeviceLifecycleState.Active)
            {
                State = DeviceLifecycleState.Pending;
            }
        }
    }

    public DateTime? LastVerifiedServerTimeUtc
    {
        get => LastActiveAtUtc;
        set => LastActiveAtUtc = value;
    }

    public DateTime CreatedUtc
    {
        get => CreatedAtUtc;
        set => CreatedAtUtc = value;
    }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public Device() { }

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
            HardwareId = hardwareFingerprint,
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

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}
