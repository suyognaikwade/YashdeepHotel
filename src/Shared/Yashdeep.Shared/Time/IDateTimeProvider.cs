using System;

namespace Yashdeep.Shared.Time;

/// <summary>
/// Abstraction for date and time retrieval to support deterministic testing and server-authoritative time handling.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current date and time in UTC format.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current Unix timestamp in seconds.
    /// </summary>
    long UtcUnixTimestampSeconds => new DateTimeOffset(UtcNow).ToUnixTimeSeconds();

    /// <summary>
    /// Gets the current Unix timestamp in milliseconds.
    /// </summary>
    long UtcUnixTimestampMilliseconds => new DateTimeOffset(UtcNow).ToUnixTimeMilliseconds();
}
