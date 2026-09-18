using System;
using Yashdeep.Shared.Identifiers;

namespace Yashdeep.Shared.Primitives;

/// <summary>
/// Base abstraction for domain entities with a strongly typed identifier.
/// </summary>
/// <typeparam name="TId">The strongly typed identifier type.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : struct, IStronglyTypedId
{
    public TId Id { get; protected set; }
    public DateTime CreatedUtc { get; protected set; }
    public DateTime? ModifiedUtc { get; protected set; }

    protected Entity(TId id, DateTime createdUtc)
    {
        Id = id;
        CreatedUtc = createdUtc;
    }

    protected Entity()
    {
        Id = default;
        CreatedUtc = DateTime.UtcNow;
    }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
