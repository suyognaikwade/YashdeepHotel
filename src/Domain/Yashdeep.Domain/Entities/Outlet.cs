using System;
using System.Collections.Generic;
using Yashdeep.Domain.Common;
using Yashdeep.Domain.Enums;

namespace Yashdeep.Domain.Entities;

public class Outlet : ITenantScopedEntity, IAuditableEntity, ISoftDeletableEntity, IConcurrencyAwareEntity
{
    public Guid OutletId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public OutletType Type { get; set; } = OutletType.DiningRoom;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public uint ConcurrencyToken { get; set; }

    public Branch? Branch { get; set; }
    public Tenant? Tenant { get; set; }
    public ICollection<Terminal> Terminals { get; set; } = new List<Terminal>();
}
