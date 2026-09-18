using System;
using System.Collections.Generic;

namespace Yashdeep.Shared.Primitives;

/// <summary>
/// Base abstraction for Aggregate Roots holding uncommitted domain events.
/// </summary>
/// <typeparam name="TId">The strongly typed identifier type.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : struct, Identifiers.IStronglyTypedId
{
    private readonly List<IDomainEvent> _domainEvents = new();

    protected AggregateRoot(TId id, DateTime createdUtc)
        : base(id, createdUtc)
    {
    }

    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Gets read-only collection of uncommitted domain events.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Raises a domain event.
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all raised domain events.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
