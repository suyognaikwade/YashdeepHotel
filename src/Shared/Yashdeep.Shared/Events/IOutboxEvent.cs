namespace Yashdeep.Shared.Events;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime CreatedUtc { get; }
}

public interface IOutboxEvent : IDomainEvent
{
    string AggregateType { get; }
    Guid AggregateId { get; }
    Guid TenantId { get; }
    Guid BranchId { get; }
    Guid DeviceId { get; }
    string EventType { get; }
    int EventVersion { get; }
}

public abstract record OutboxEventBase : IOutboxEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public abstract string AggregateType { get; }
    public Guid AggregateId { get; init; }
    public Guid TenantId { get; init; }
    public Guid BranchId { get; init; }
    public Guid DeviceId { get; init; }
    public abstract string EventType { get; }
    public virtual int EventVersion => 1;
}
