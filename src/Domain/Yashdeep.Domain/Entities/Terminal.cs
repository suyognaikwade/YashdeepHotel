namespace Yashdeep.Domain.Entities;

/// <summary>
/// Physical POS workstation / terminal entity assigned to an outlet.
/// </summary>
public class Terminal
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OutletId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
