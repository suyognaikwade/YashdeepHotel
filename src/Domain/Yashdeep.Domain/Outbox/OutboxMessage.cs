using Yashdeep.Shared.Events;

namespace Yashdeep.Domain.Outbox;

public class OutboxMessage
{
    // EF Core / Deserialization Constructor
    private OutboxMessage()
    {
        AggregateType = string.Empty;
        EventType = string.Empty;
        PayloadJson = string.Empty;
        PayloadHash = string.Empty;
    }

    public OutboxMessage(
        Guid eventId,
        string aggregateType,
        Guid aggregateId,
        Guid tenantId,
        Guid branchId,
        Guid deviceId,
        long sequenceNumber,
        string eventType,
        int eventVersion,
        DateTime createdUtc,
        string payloadJson)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(aggregateType))
            throw new ArgumentException("AggregateType is required.", nameof(aggregateType));
        if (aggregateId == Guid.Empty)
            throw new ArgumentException("AggregateId cannot be empty.", nameof(aggregateId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId cannot be empty.", nameof(branchId));
        if (deviceId == Guid.Empty)
            throw new ArgumentException("DeviceId cannot be empty.", nameof(deviceId));
        if (sequenceNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber), "Sequence number must be positive.");
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType is required.", nameof(eventType));
        if (eventVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventVersion), "EventVersion must be positive.");
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("PayloadJson cannot be empty.", nameof(payloadJson));

        EventId = eventId;
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        TenantId = tenantId;
        BranchId = branchId;
        DeviceId = deviceId;
        SequenceNumber = sequenceNumber;
        EventType = eventType;
        EventVersion = eventVersion;
        CreatedUtc = createdUtc == default ? DateTime.UtcNow : createdUtc;
        PayloadJson = payloadJson;
        PayloadHash = PayloadHasher.ComputeSha256Hash(payloadJson);
        Status = OutboxStatus.Pending;
        RetryCount = 0;
    }

    public static OutboxMessage FromEvent<TEvent>(TEvent domainEvent, long sequenceNumber, string payloadJson)
        where TEvent : IOutboxEvent
    {
        if (domainEvent == null) throw new ArgumentNullException(nameof(domainEvent));

        return new OutboxMessage(
            domainEvent.EventId,
            domainEvent.AggregateType,
            domainEvent.AggregateId,
            domainEvent.TenantId,
            domainEvent.BranchId,
            domainEvent.DeviceId,
            sequenceNumber,
            domainEvent.EventType,
            domainEvent.EventVersion,
            domainEvent.CreatedUtc,
            payloadJson);
    }

    // Identity & Hierarchy Context
    public Guid EventId { get; private set; }
    public string AggregateType { get; private set; }
    public Guid AggregateId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid DeviceId { get; private set; }
    public long SequenceNumber { get; private set; }

    // Event Classification
    public string EventType { get; private set; }
    public int EventVersion { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    // Payload & Integrity Checksum
    public string PayloadJson { get; private set; }
    public string PayloadHash { get; private set; }

    // Delivery State Machine
    public OutboxStatus Status { get; private set; }

    // Safe Retry Metadata
    public int RetryCount { get; private set; }
    public DateTime? LastAttemptedUtc { get; private set; }
    public DateTime? NextRetryUtc { get; private set; }

    // Failure / Dead-Letter Metadata
    public string? LastError { get; private set; }
    public string? StackTrace { get; private set; }
    public DateTime? DeadLetteredUtc { get; private set; }

    // State Transitions
    public void MarkInFlight(DateTime attemptedUtc)
    {
        if (Status == OutboxStatus.Synced || Status == OutboxStatus.DeadLetter)
        {
            throw new InvalidOperationException($"Cannot transition outbox event from {Status} to InFlight.");
        }

        Status = OutboxStatus.InFlight;
        LastAttemptedUtc = attemptedUtc;
    }

    public void MarkSynced()
    {
        if (Status == OutboxStatus.DeadLetter)
        {
            throw new InvalidOperationException("Cannot transition dead-lettered outbox event to Synced.");
        }

        Status = OutboxStatus.Synced;
        LastError = null;
        StackTrace = null;
    }

    public void RecordFailure(string error, string? stackTrace, DateTime failedUtc, TimeSpan? retryBackoff = null, int maxRetries = 5)
    {
        if (Status == OutboxStatus.Synced)
        {
            throw new InvalidOperationException("Cannot record failure on already synced outbox event.");
        }

        RetryCount++;
        LastAttemptedUtc = failedUtc;
        LastError = error;
        StackTrace = stackTrace;

        if (RetryCount >= maxRetries)
        {
            MarkDeadLetter(failedUtc, $"Exceeded maximum retries ({maxRetries}). Last error: {error}");
        }
        else
        {
            Status = OutboxStatus.Failed;
            var backoff = retryBackoff ?? TimeSpan.FromSeconds(Math.Pow(2, RetryCount));
            NextRetryUtc = failedUtc.Add(backoff);
        }
    }

    public void MarkDeadLetter(DateTime deadLetteredUtc, string reason)
    {
        Status = OutboxStatus.DeadLetter;
        DeadLetteredUtc = deadLetteredUtc;
        LastError = reason;
        NextRetryUtc = null;
    }

    public bool VerifyPayloadIntegrity()
    {
        if (string.IsNullOrEmpty(PayloadJson) || string.IsNullOrEmpty(PayloadHash))
        {
            return false;
        }

        string computed = PayloadHasher.ComputeSha256Hash(PayloadJson);
        return string.Equals(computed, PayloadHash, StringComparison.OrdinalIgnoreCase);
    }
}
