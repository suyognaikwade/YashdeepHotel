namespace Yashdeep.Domain.Entities;

/// <summary>
/// Physical property or location branch entity under a tenant.
/// </summary>
public class Branch
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public ICollection<Outlet> Outlets { get; set; } = new List<Outlet>();
}
