using System.Security.Cryptography;
using System.Text;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Domain.Entities.Sync;

public class OutboxMessage
{
    public Guid EventId { get; private set; }
    public string EventType { get; private set; }
    public string AggregateType { get; private set; }
    public Guid AggregateId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid DeviceId { get; private set; }
    public long SequenceNumber { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public string PayloadJson { get; private set; }
    public string PayloadHash { get; private set; }
    public OutboxMessageStatus Status { get; private set; }

    private OutboxMessage()
    {
        EventType = string.Empty;
        AggregateType = string.Empty;
        PayloadJson = string.Empty;
        PayloadHash = string.Empty;
    }

    public OutboxMessage(
        Guid eventId,
        string eventType,
        string aggregateType,
        Guid aggregateId,
        Guid tenantId,
        Guid branchId,
        Guid deviceId,
        long sequenceNumber,
        string payloadJson)
    {
        EventId = eventId == Guid.Empty ? Guid.NewGuid() : eventId;
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        AggregateType = aggregateType ?? throw new ArgumentNullException(nameof(aggregateType));
        AggregateId = aggregateId;
        TenantId = tenantId;
        BranchId = branchId;
        DeviceId = deviceId;
        SequenceNumber = sequenceNumber;
        CreatedUtc = DateTime.UtcNow;
        PayloadJson = payloadJson ?? "{}";
        PayloadHash = ComputeSha256Hash(PayloadJson);
        Status = OutboxMessageStatus.Pending;
    }

    public void MarkUploaded()
    {
        Status = OutboxMessageStatus.Uploaded;
    }

    private static string ComputeSha256Hash(string rawData)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
