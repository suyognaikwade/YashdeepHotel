namespace Yashdeep.Domain.Entities;

/// <summary>
/// Operational cost center outlet entity (e.g., Main Dining, AC Bar, Permit Room, Front Desk).
/// </summary>
public class Outlet
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string OutletType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public Branch? Branch { get; set; }
}
