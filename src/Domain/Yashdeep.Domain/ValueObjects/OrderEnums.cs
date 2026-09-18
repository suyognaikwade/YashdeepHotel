namespace Yashdeep.Domain.ValueObjects;

public enum SectionTier
{
    Hall = 1,
    Restaurant = 2,
    Family = 3,
    Ac = 4,
    Vip = 5,
    Parcel = 6
}

public enum OrderType
{
    DineIn = 1,
    TakeawayParcel = 2,
    RoomService = 3
}

public enum OrderStatus
{
    Open = 1,
    Billed = 2,
    Completed = 3,
    Cancelled = 4
}

public enum DepartmentType
{
    Kitchen = 1,
    Bar = 2,
    Beverage = 3
}

public enum KotTicketType
{
    KotKitchen = 1,
    BotBar = 2
}
