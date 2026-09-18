using System;

namespace Yashdeep.Shared.Primitives;

/// <summary>
/// Contract interface for domain events generated within domain aggregates.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Unique event identifier.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Timestamp in UTC when the event occurred.
    /// </summary>
    DateTime OccurredUtc { get; }
}
