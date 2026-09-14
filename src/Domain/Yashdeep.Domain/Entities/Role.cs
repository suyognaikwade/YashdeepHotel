namespace Yashdeep.Domain.Entities;

public class Role
{
    public Guid RoleId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    private readonly List<Permission> _permissions = new();
    public IReadOnlyCollection<Permission> Permissions => _permissions.AsReadOnly();

    private Role() { }

    public Role(Guid roleId, Guid tenantId, string name, string description = "")
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));

        RoleId = roleId == Guid.Empty ? Guid.NewGuid() : roleId;
        TenantId = tenantId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
    }

    public void AddPermission(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        if (!_permissions.Any(p => p.Code == permission.Code))
        {
            _permissions.Add(permission);
        }
    }
}
