namespace Yashdeep.Domain.Enums;

public enum TenantStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    SoftDeleted = 3,
    Terminated = 4
}

public enum OutletType
{
    DiningRoom = 1,
    AcBar = 2,
    PermitRoom = 3,
    FrontDesk = 4,
    Kitchen = 5,
    ParcelCounter = 6,
    RoomService = 7
}

public enum DeviceType
{
    MainPosTerminal = 1,
    WaiterHandheld = 2,
    KitchenDisplaySystem = 3,
    BarCounterPos = 4,
    AdminManagementPortal = 5
}
