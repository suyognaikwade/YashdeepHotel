using System;
using System.Collections.Generic;
using Yashdeep.Domain.Common;

namespace Yashdeep.Domain.Entities;

public class Branch : ITenantScopedEntity, IAuditableEntity, ISoftDeletableEntity, IConcurrencyAwareEntity
{
    public Guid BranchId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrganizationId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public uint ConcurrencyToken { get; set; }

    public Organization? Organization { get; set; }
    public Tenant? Tenant { get; set; }
    public ICollection<Outlet> Outlets { get; set; } = new List<Outlet>();
    public ICollection<Terminal> Terminals { get; set; } = new List<Terminal>();
}
