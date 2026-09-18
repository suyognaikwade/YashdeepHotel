using System;
using System.Collections.Generic;
using Yashdeep.Domain.Common;

namespace Yashdeep.Domain.Entities;

public class Organization : ITenantScopedEntity, IAuditableEntity, ISoftDeletableEntity, IConcurrencyAwareEntity
{
    public Guid OrganizationId { get; set; }
    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LegalEntityName { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public string? StateExciseLicenseNo { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public uint ConcurrencyToken { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
