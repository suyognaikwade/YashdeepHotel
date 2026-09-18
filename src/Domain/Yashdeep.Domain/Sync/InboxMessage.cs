using System;

namespace Yashdeep.Domain.Sync
{
    public enum InboxStatus
    {
        Processing = 1,
        Processed = 2,
        PayloadMismatch = 3,
        Rejected = 4,
        Failed = 5
    }

    public sealed class InboxMessage
    {
        public Guid EventId { get; set; }
        public Guid TenantId { get; set; }
        public Guid BranchId { get; set; }
        public Guid DeviceId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string AggregateType { get; set; } = string.Empty;
        public Guid AggregateId { get; set; }
        public long SequenceNumber { get; set; }
        public string PayloadHash { get; set; } = string.Empty;
        public InboxStatus Status { get; set; } = InboxStatus.Processing;
        public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAtUtc { get; set; }
        public string? ResponsePayloadJson { get; set; }
        public string? ErrorMessage { get; set; }
        public string? DiagnosticsJson { get; set; }
    }
}
