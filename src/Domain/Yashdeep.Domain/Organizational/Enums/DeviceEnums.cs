namespace Yashdeep.Domain.Organizational.Enums;

public enum DeviceScope
{
    TenantLevel = 1,
    BranchLevel = 2,
    OutletLevel = 3,
    TerminalLevel = 4
}

public enum DeviceStatus
{
    PendingProvisioning = 1,
    Active = 2,
    Suspended = 3,
    Revoked = 4
}
