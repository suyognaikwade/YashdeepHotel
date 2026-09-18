namespace Yashdeep.Shared.Entitlements;

/// <summary>
/// Strongly typed capability identifiers across modular product editions (Bar &amp; Restaurant, Hotel, Hybrid, Custom).
/// Used by ICapabilityEvaluator for dynamic authorization across application boundaries.
/// </summary>
public static class CapabilityIdentifier
{
    // Bar & Restaurant Capabilities
    public const string BarRestaurant_Core = "Capability.BarRestaurant.Core";
    public const string BarRestaurant_KotBot = "Capability.BarRestaurant.KotBot";
    public const string BarRestaurant_TableManagement = "Capability.BarRestaurant.TableManagement";
    public const string BarRestaurant_SectionPricing = "Capability.BarRestaurant.SectionPricing";
    public const string BarRestaurant_PegDispensing = "Capability.BarRestaurant.PegDispensing";
    public const string BarRestaurant_ExciseFl3Compliance = "Capability.BarRestaurant.ExciseFl3Compliance";
    public const string BarRestaurant_DayEndAudit = "Capability.BarRestaurant.DayEndAudit";

    // Hotel & Lodging Capabilities
    public const string Hotel_Core = "Capability.Hotel.Core";
    public const string Hotel_GuestCheckInCheckOut = "Capability.Hotel.GuestCheckInCheckOut";
    public const string Hotel_RoomTariffMaster = "Capability.Hotel.RoomTariffMaster";
    public const string Hotel_ReservationLedger = "Capability.Hotel.ReservationLedger";
    public const string Hotel_HousekeepingStatus = "Capability.Hotel.HousekeepingStatus";
    public const string Hotel_NightAudit = "Capability.Hotel.NightAudit";

    // Hybrid & Cross-Outlet Capabilities
    public const string Hybrid_CrossOutletBilling = "Capability.Hybrid.CrossOutletBilling";
    public const string Hybrid_PostToRoom = "Capability.Hybrid.PostToRoom";

    // Advanced Enterprise Capabilities
    public const string Enterprise_MultiBranchAggregation = "Capability.Enterprise.MultiBranchAggregation";
    public const string Enterprise_AdvancedAnalytics = "Capability.Enterprise.AdvancedAnalytics";
    public const string Enterprise_CustomEntitlements = "Capability.Enterprise.CustomEntitlements";
}
