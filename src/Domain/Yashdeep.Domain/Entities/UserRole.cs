namespace Yashdeep.Domain.Entities;

/// <summary>
/// Many-to-many join entity between User and Role.
/// </summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid TenantId { get; set; }

    public User? User { get; set; }
    public Role? Role { get; set; }
}
