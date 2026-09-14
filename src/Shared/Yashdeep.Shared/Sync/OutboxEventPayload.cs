using System;
using Yashdeep.Shared.Contracts;
using Yashdeep.Shared.Identifiers;

namespace Yashdeep.Shared.Sync;

/// <summary>
/// Standard Outbox/Inbox synchronization payload contract representing an event generated on Edge or Cloud.
/// Independent of EF Core or database-specific storage types.
/// </summary>
[ContractVersion(ApiVersionInfo.OutboxProtocolVersion, "Synchronization")]
public class OutboxEventPayload
{
    /// <summary>
    /// Unique event identifier (UUID v4 / Sequential Guid).
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Fully qualified event type name (e.g. OrderPlacedEvent).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Aggregate root type (e.g. Order, Bill, InventoryItem).
    /// </summary>
    public string AggregateType { get; set; } = string.Empty;

    /// <summary>
    /// Aggregate root unique identifier.
    /// </summary>
    public AggregateId AggregateId { get; set; }

    /// <summary>
    /// Multi-tenant boundary identifier.
    /// </summary>
    public TenantId TenantId { get; set; }

    /// <summary>
    /// Physical branch boundary identifier.
    /// </summary>
    public BranchId BranchId { get; set; }

    /// <summary>
    /// Registering edge device identifier.
    /// </summary>
    public DeviceId DeviceId { get; set; }

    /// <summary>
    /// Monotonically increasing sequence number per device.
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Schema/Contract version of the event payload (e.g. "1.0").
    /// </summary>
    public string EventVersion { get; set; } = "1.0";

    /// <summary>
    /// Monotonic UTC timestamp when the event was originally created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Serialized JSON string containing the domain event data.
    /// </summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>
    /// Cryptographic Ed25519 signature of the payload (Hex string) for integrity validation.
    /// </summary>
    public string PayloadSignature { get; set; } = string.Empty;
}
