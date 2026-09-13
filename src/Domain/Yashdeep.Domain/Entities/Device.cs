namespace Yashdeep.Domain.Entities;

/// <summary>
/// Hardware device instance entity tracking registered edge hardware.
/// </summary>
public class Device
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string HardwareId { get; set; } = string.Empty;
    public string DeviceCertificate { get; set; } = string.Empty;
    public bool IsRegistered { get; set; } = false;
    public DateTime? LastVerifiedServerTimeUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
