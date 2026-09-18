using System;

namespace Yashdeep.Domain.Common;

/// <summary>
/// Indicates that an entity belongs to a specific tenant boundary.
/// </summary>
public interface ITenantScopedEntity
{
    Guid TenantId { get; set; }
}

/// <summary>
/// Indicates that an entity captures audit metadata (creation and modification UTC timestamps).
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>
/// Indicates that an entity supports soft deletion.
/// </summary>
public interface ISoftDeletableEntity
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
}

/// <summary>
/// Indicates that an entity supports optimistic concurrency protection using a concurrency token (xmin / byte[] / rowversion).
/// </summary>
public interface IConcurrencyAwareEntity
{
    uint ConcurrencyToken { get; set; }
}
