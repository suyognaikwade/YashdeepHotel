using System;
using System.Collections.Generic;
using Yashdeep.Shared.Contracts;
using Yashdeep.Shared.Identifiers;

namespace Yashdeep.Shared.Sync;

/// <summary>
/// Status of an individual sync event processed by the Inbox synchronization engine.
/// </summary>
public enum SyncAckStatus
{
    Success = 1,
    DuplicateReplayed = 2,
    ValidationError = 3,
    ConflictDetected = 4,
    QuarantinedToDlq = 5
}

/// <summary>
/// Synchronization request batch uploaded by an edge POS terminal to cloud Inbox.
/// </summary>
[ContractVersion(ApiVersionInfo.OutboxProtocolVersion, "Synchronization")]
public class SyncBatchRequest
{
    public DeviceId DeviceId { get; set; }
    public TenantId TenantId { get; set; }
    public BranchId BranchId { get; set; }
    public string BatchId { get; set; } = Guid.NewGuid().ToString("D");
    public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    public List<OutboxEventPayload> Events { get; set; } = new();
}

/// <summary>
/// Individual acknowledgment item in a sync response batch.
/// </summary>
public class SyncAckItem
{
    public Guid EventId { get; set; }
    public long SequenceNumber { get; set; }
    public SyncAckStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Synchronization response batch returned by cloud server to edge POS terminal.
/// </summary>
[ContractVersion(ApiVersionInfo.OutboxProtocolVersion, "Synchronization")]
public class SyncBatchResponse
{
    public string BatchId { get; set; } = string.Empty;
    public DateTime ProcessedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ServerAuthoritativeTimeUtc { get; set; } = DateTime.UtcNow;
    public List<SyncAckItem> Acknowledgments { get; set; } = new();
    public bool AllSucceeded => Acknowledgments.TrueForAll(a => a.Status == SyncAckStatus.Success || a.Status == SyncAckStatus.DuplicateReplayed);
}
