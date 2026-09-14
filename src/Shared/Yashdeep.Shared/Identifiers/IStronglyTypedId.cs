using System;

namespace Yashdeep.Shared.Identifiers;

/// <summary>
/// Contract interface for strongly typed domain identifiers.
/// </summary>
public interface IStronglyTypedId : IEquatable<IStronglyTypedId>
{
    /// <summary>
    /// Gets the underlying Guid value of the identifier.
    /// </summary>
    Guid Value { get; }

    /// <summary>
    /// Gets whether the identifier is empty (Guid.Empty).
    /// </summary>
    bool IsEmpty => Value == Guid.Empty;
}
