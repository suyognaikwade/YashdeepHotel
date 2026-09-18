using Yashdeep.Domain.Contracts;

namespace Yashdeep.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public List<Organization> Organizations { get; set; } = new();
    public List<Branch> Branches { get; set; } = new();
    public List<User> Users { get; set; } = new();
}

public class Organization : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string LegalName { get; set; } = string.Empty;
    public string TaxIdentifier { get; set; } = string.Empty;

    public List<Branch> Branches { get; set; } = new();
}

public class Branch : IBranchEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId
    {
        get => Id;
        set => Id = value;
    }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public List<Outlet> Outlets { get; set; } = new();
    public List<Terminal> Terminals { get; set; } = new();
}

public class Outlet : IOutletEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OutletId
    {
        get => Id;
        set => Id = value;
    }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Terminal : IBranchEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public List<Device> Devices { get; set; } = new();
}

public class Device : IBranchEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid TerminalId { get; set; }
    public string DeviceIdentifier { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class User : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsSystemAdmin { get; set; }
    public bool HasOrganizationWideAccess { get; set; }

    public List<UserBranchAssignment> BranchAssignments { get; set; } = new();
    public List<UserRoleAssignment> RoleAssignments { get; set; } = new();
}

public class UserBranchAssignment
{
    public Guid UserId { get; set; }
    public Guid BranchId { get; set; }
}

public class UserRoleAssignment
{
    public Guid UserId { get; set; }
    public string RoleName { get; set; } = string.Empty;
}
