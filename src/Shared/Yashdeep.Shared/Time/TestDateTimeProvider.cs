using System;

namespace Yashdeep.Shared.Time;

/// <summary>
/// Deterministic time provider for unit testing. Allows setting and advancing fixed UTC timestamps.
/// </summary>
public sealed class TestDateTimeProvider : IDateTimeProvider
{
    private DateTime _utcNow;

    public TestDateTimeProvider(DateTime? initialUtcNow = null)
    {
        _utcNow = initialUtcNow ?? new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc);
    }

    public DateTime UtcNow => _utcNow;

    /// <summary>
    /// Sets the deterministic UTC timestamp.
    /// </summary>
    public void SetUtcNow(DateTime utcNow)
    {
        _utcNow = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    /// <summary>
    /// Advances the deterministic timestamp by the specified duration.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
    }
}
