namespace Yashdeep.Domain.Entities;

/// <summary>
/// Root organizational tenant entity stored in local persistence.
/// Represents the subscription / business entity level.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
