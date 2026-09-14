namespace Yashdeep.Application.Connectivity;

using Yashdeep.Domain.Connectivity;

/// <summary>
/// Server-authoritative time abstraction.
/// Server is authoritative for connectivity and entitlement decisions.
/// Client system clock must not be trusted as sole source.
/// </summary>
public interface IServerTimeProvider
{
    /// <summary>
    /// Returns current authoritative server time if online, or estimates it using last verified server check-in + elapsed monotonic ticks.
    /// </summary>
    DateTime GetAuthoritativeTimeUtc();

    /// <summary>
    /// Process a verified server check-in response to update authoritative time state.
    /// </summary>
    void SynchronizeServerTime(DateTime serverTimeUtc);
}

/// <summary>
/// Monotonic clock provider tracking hardware uptime/ticks immune to system wall-clock modification.
/// </summary>
public interface IMonotonicClock
{
    /// <summary>
    /// Monotonically increasing tick count (in milliseconds or Stopwatch ticks).
    /// </summary>
    long GetMonotonicTicks();
}

/// <summary>
/// Clock tamper detector abstraction detecting client system clock rollbacks or abnormal drifts.
/// </summary>
public interface IClockTamperDetector
{
    /// <summary>
    /// Detects if system clock has been moved backward or altered relative to last verified server check-in.
    /// </summary>
    bool IsClockTampered(DateTime currentLocalTimeUtc, long currentMonotonicTicks, DeviceCheckInRecord? lastCheckIn, TimeSpan maxAllowedSkew);
}

/// <summary>
/// Secure local recording of last verified server check-in.
/// </summary>
public interface ICheckInStore
{
    DeviceCheckInRecord? GetLastCheckIn(string deviceId);
    void SaveCheckIn(DeviceCheckInRecord checkIn);
}
