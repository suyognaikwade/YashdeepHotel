namespace Yashdeep.Domain.Entities;

public class Permission
{
    public Guid PermissionId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    private Permission() { }

    public Permission(Guid permissionId, string code, string description = "")
    {
        PermissionId = permissionId == Guid.Empty ? Guid.NewGuid() : permissionId;
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Description = description ?? string.Empty;
    }
}
