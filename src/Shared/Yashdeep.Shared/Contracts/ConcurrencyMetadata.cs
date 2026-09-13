using System;

namespace Yashdeep.Shared.Contracts;

/// <summary>
/// Optimistic concurrency metadata holding version, ETag, and row version tracking.
/// </summary>
public record ConcurrencyMetadata(
    long Version,
    string? ETag = null,
    byte[]? RowVersion = null)
{
    public static ConcurrencyMetadata Initial => new(1);
}
