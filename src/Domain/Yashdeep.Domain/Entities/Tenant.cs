using System;
using System.Collections.Generic;
using Yashdeep.Domain.Common;
using Yashdeep.Domain.Enums;

namespace Yashdeep.Domain.Entities;

public class Tenant : IAuditableEntity, ISoftDeletableEntity, IConcurrencyAwareEntity
{
    public Guid TenantId { get; set; }
    public string LegalName { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public string? GSTIN { get; set; }
    public string? ExciseLicenseNumber { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public uint ConcurrencyToken { get; set; }

    public ICollection<Organization> Organizations { get; set; } = new List<Organization>();
}
