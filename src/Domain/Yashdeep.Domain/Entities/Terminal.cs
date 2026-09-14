using System;
using System.Collections.Generic;
using Yashdeep.Domain.Common;

namespace Yashdeep.Domain.Entities;

public class Terminal : ITenantScopedEntity, IAuditableEntity, ISoftDeletableEntity, IConcurrencyAwareEntity
{
    public Guid TerminalId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? OutletId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public uint ConcurrencyToken { get; set; }

    public Branch? Branch { get; set; }
    public Outlet? Outlet { get; set; }
    public Tenant? Tenant { get; set; }
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}
