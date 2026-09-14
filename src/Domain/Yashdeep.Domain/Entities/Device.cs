using System;
using Yashdeep.Domain.Common;
using Yashdeep.Domain.Enums;

namespace Yashdeep.Domain.Entities;

public class Device : ITenantScopedEntity, IAuditableEntity, ISoftDeletableEntity, IConcurrencyAwareEntity
{
    public Guid DeviceId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? OutletId { get; set; }
    public Guid TerminalId { get; set; }

    public string DeviceIdentifier { get; set; } = string.Empty;
    public string HardwareFingerprint { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DeviceType Type { get; set; } = DeviceType.MainPosTerminal;
    public string PublicEd25519Key { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public uint ConcurrencyToken { get; set; }

    public Terminal? Terminal { get; set; }
    public Tenant? Tenant { get; set; }
}
