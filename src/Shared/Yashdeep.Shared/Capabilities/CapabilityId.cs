namespace Yashdeep.Shared.Capabilities;

public readonly record struct CapabilityId
{
    public string Value { get; }

    public CapabilityId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Capability identifier cannot be empty or null.", nameof(value));
        }

        Value = value.Trim().ToLowerInvariant();
    }

    public override string ToString() => Value;

    public static implicit operator string(CapabilityId capabilityId) => capabilityId.Value;
    public static implicit operator CapabilityId(string value) => new(value);

    // Core POS & Dining Capabilities
    public static readonly CapabilityId PosBilling = new("pos_billing");
    public static readonly CapabilityId KotRouting = new("kot_routing");
    public static readonly CapabilityId BilingualMarathiKot = new("bilingual_marathi_kot");
    public static readonly CapabilityId DynamicUpiQr = new("dynamic_upi_qr");
    public static readonly CapabilityId OutboxSqliteSync = new("outbox_sqlite_sync");

    // Bar & Excise Capabilities
    public static readonly CapabilityId MultiTierInventory = new("multi_tier_inventory");
    public static readonly CapabilityId ExciseFl3Compliance = new("excise_fl3_compliance");
    public static readonly CapabilityId LoosePegDispensing = new("loose_peg_dispensing");

    // Hotel & Hospitality Capabilities
    public static readonly CapabilityId HotelRooms = new("hotel_rooms");
    public static readonly CapabilityId Housekeeping = new("housekeeping");
    public static readonly CapabilityId RoomServicePosting = new("room_service_posting");

    // Enterprise & Integration Capabilities
    public static readonly CapabilityId MultiBranchStockTransfer = new("multi_branch_stock_transfer");
    public static readonly CapabilityId CentralizedMasterMenu = new("centralized_master_menu");
    public static readonly CapabilityId CustomApiWebhooks = new("custom_api_webhooks");
    public static readonly CapabilityId AdvancedAnalytics = new("advanced_analytics");
    public static readonly CapabilityId QuestPdfReporting = new("questpdf_reporting");
}
