namespace Yashdeep.Domain.Entities;

/// <summary>
/// Local edge database operational metadata and schema version tracking entity.
/// </summary>
public class LocalSettings
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public DateTime InitializedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
