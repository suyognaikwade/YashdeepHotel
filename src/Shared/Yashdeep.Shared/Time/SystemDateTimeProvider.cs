using System;

namespace Yashdeep.Shared.Time;

/// <summary>
/// Real-world implementation of IDateTimeProvider that uses DateTime.UtcNow.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    /// <summary>
    /// Singleton instance of SystemDateTimeProvider.
    /// </summary>
    public static readonly SystemDateTimeProvider Instance = new();

    public DateTime UtcNow => DateTime.UtcNow;
}
