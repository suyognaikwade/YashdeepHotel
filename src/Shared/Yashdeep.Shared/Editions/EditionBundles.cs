namespace Yashdeep.Shared.Editions;

using Yashdeep.Shared.Capabilities;

public static class EditionBundles
{
    public const string BarAndRestaurantEdition = "BAR_AND_RESTAURANT";
    public const string HotelHospitalityEdition = "HOTEL_HOSPITALITY";
    public const string HotelBarRestaurantHybridEdition = "HOTEL_BAR_RESTAURANT_HYBRID";
    public const string CustomBundle = "CUSTOM_BUNDLE";

    private static readonly HashSet<CapabilityId> BarAndRestaurantCapabilities = new()
    {
        CapabilityId.PosBilling,
        CapabilityId.KotRouting,
        CapabilityId.BilingualMarathiKot,
        CapabilityId.DynamicUpiQr,
        CapabilityId.OutboxSqliteSync,
        CapabilityId.MultiTierInventory,
        CapabilityId.ExciseFl3Compliance,
        CapabilityId.LoosePegDispensing,
        CapabilityId.QuestPdfReporting,
        CapabilityId.AdvancedAnalytics
    };

    private static readonly HashSet<CapabilityId> HotelHospitalityCapabilities = new()
    {
        CapabilityId.HotelRooms,
        CapabilityId.Housekeeping,
        CapabilityId.OutboxSqliteSync,
        CapabilityId.QuestPdfReporting,
        CapabilityId.AdvancedAnalytics
    };

    private static readonly HashSet<CapabilityId> HotelBarRestaurantHybridCapabilities = new()
    {
        CapabilityId.PosBilling,
        CapabilityId.KotRouting,
        CapabilityId.BilingualMarathiKot,
        CapabilityId.DynamicUpiQr,
        CapabilityId.OutboxSqliteSync,
        CapabilityId.MultiTierInventory,
        CapabilityId.ExciseFl3Compliance,
        CapabilityId.LoosePegDispensing,
        CapabilityId.HotelRooms,
        CapabilityId.Housekeeping,
        CapabilityId.RoomServicePosting,
        CapabilityId.QuestPdfReporting,
        CapabilityId.AdvancedAnalytics
    };

    public static IReadOnlySet<CapabilityId> GetCapabilitiesForEdition(string editionCode)
    {
        return editionCode.ToUpperInvariant() switch
        {
            BarAndRestaurantEdition => BarAndRestaurantCapabilities,
            HotelHospitalityEdition => HotelHospitalityCapabilities,
            HotelBarRestaurantHybridEdition => HotelBarRestaurantHybridCapabilities,
            CustomBundle => new HashSet<CapabilityId>(),
            _ => new HashSet<CapabilityId>()
        };
    }
}
