namespace Yashdeep.Shared.Entitlements;

using System.Text.Json.Serialization;

public sealed class EntitlementCapacityLimits
{
    [JsonPropertyName("max_locations")]
    public int MaxLocations { get; set; } = 1;

    [JsonPropertyName("max_devices_per_location")]
    public int MaxDevicesPerLocation { get; set; } = 2;

    [JsonPropertyName("max_users")]
    public int MaxUsers { get; set; } = 5;

    [JsonPropertyName("max_sections")]
    public int MaxSections { get; set; } = 5;

    [JsonPropertyName("max_tables_per_section")]
    public int MaxTablesPerSection { get; set; } = 20;

    [JsonPropertyName("max_offline_days")]
    public int MaxOfflineDays { get; set; } = 30;
}

public sealed class SignedEntitlementTokenPayload
{
    [JsonPropertyName("iss")]
    public string Issuer { get; set; } = "https://auth.yashdeep-saas.com";

    [JsonPropertyName("sub")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("aud")]
    public string Audience { get; set; } = "yashdeep_pos_client";

    [JsonPropertyName("jti")]
    public string TokenId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("iat")]
    public long IssuedAtUnix { get; set; }

    [JsonPropertyName("nbf")]
    public long NotBeforeUnix { get; set; }

    [JsonPropertyName("exp")]
    public long ExpirationUnix { get; set; }

    [JsonPropertyName("offline_grace_exp")]
    public long OfflineGraceExpirationUnix { get; set; }

    [JsonPropertyName("tenant_id")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("organization_id")]
    public string OrganizationId { get; set; } = string.Empty;

    [JsonPropertyName("plan_id")]
    public string PlanId { get; set; } = string.Empty;

    [JsonPropertyName("plan_code")]
    public string PlanCode { get; set; } = string.Empty;

    [JsonPropertyName("subscription_status")]
    public string SubscriptionStatus { get; set; } = "Active";

    [JsonPropertyName("hardware_fingerprint")]
    public string HardwareFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("limits")]
    public EntitlementCapacityLimits Limits { get; set; } = new();

    [JsonPropertyName("capabilities")]
    public HashSet<string> Capabilities { get; set; } = new();
}

public sealed class SignedEntitlementEnvelope
{
    [JsonPropertyName("payloadJson")]
    public string PayloadJson { get; set; } = string.Empty;

    [JsonPropertyName("signatureHex")]
    public string SignatureHex { get; set; } = string.Empty;

    [JsonPropertyName("publicKeyHex")]
    public string PublicKeyHex { get; set; } = string.Empty;
}
