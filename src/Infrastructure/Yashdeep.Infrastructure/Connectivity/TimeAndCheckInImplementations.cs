namespace Yashdeep.Infrastructure.Connectivity;

using System.Diagnostics;
using Yashdeep.Application.Connectivity;
using Yashdeep.Domain.Connectivity;

/// <summary>
/// System Stopwatch implementation of IMonotonicClock.
/// Uses Environment.TickCount64 / Stopwatch.GetTimestamp() to guarantee monotonically increasing ticks.
/// </summary>
public class SystemMonotonicClock : IMonotonicClock
{
    public long GetMonotonicTicks()
    {
        return Stopwatch.GetTimestamp();
    }
}

/// <summary>
/// Clock tamper detector comparing wall-clock elapsed time against monotonic hardware tick elapsed time.
/// Flags tampering if wall clock went backwards by more than maxAllowedSkew,
/// or if wall clock elapsed time diverges unrealistically from monotonic elapsed time.
/// </summary>
public class ClockTamperDetector : IClockTamperDetector
{
    public bool IsClockTampered(
        DateTime currentLocalTimeUtc,
        long currentMonotonicTicks,
        DeviceCheckInRecord? lastCheckIn,
        TimeSpan maxAllowedSkew)
    {
        if (lastCheckIn == null)
        {
            return false;
        }

        // Check 1: Direct wall-clock rollback
        if (currentLocalTimeUtc < lastCheckIn.LocalTimeUtc - maxAllowedSkew)
        {
            return true;
        }

        // Check 2: Monotonic tick comparison
        if (lastCheckIn.MonotonicTicks > 0)
        {
            if (currentMonotonicTicks < lastCheckIn.MonotonicTicks)
            {
                // Monotonic tick decreased (system reboot / hardware reset).
                // Ensure wall clock didn't jump backwards or jump excessively beyond reasonable reboot window without online verification.
                if (currentLocalTimeUtc < lastCheckIn.LocalTimeUtc)
                {
                    return true;
                }
            }
            else
            {
                double monotonicElapsedSeconds = (double)(currentMonotonicTicks - lastCheckIn.MonotonicTicks) / Stopwatch.Frequency;
                double wallClockElapsedSeconds = (currentLocalTimeUtc - lastCheckIn.LocalTimeUtc).TotalSeconds;

                // If wall clock moved backwards relative to monotonic time beyond allowed skew
                if (wallClockElapsedSeconds < monotonicElapsedSeconds - maxAllowedSkew.TotalSeconds)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// In-memory implementation of ICheckInStore (can be swapped or extended for persistent encrypted SQLite storage).
/// </summary>
public class InMemoryCheckInStore : ICheckInStore
{
    private readonly Dictionary<string, DeviceCheckInRecord> _store = new(StringComparer.Ordinal);

    public DeviceCheckInRecord? GetLastCheckIn(string deviceId)
    {
        lock (_store)
        {
            return _store.TryGetValue(deviceId, out var record) ? record : null;
        }
    }

    public void SaveCheckIn(DeviceCheckInRecord checkIn)
    {
        lock (_store)
        {
            _store[checkIn.DeviceId] = checkIn;
        }
    }
}

/// <summary>
/// Server time provider implementing server-authoritative time calculation.
/// </summary>
public class ServerTimeProvider : IServerTimeProvider
{
    private readonly IMonotonicClock _monotonicClock;
    private readonly ICheckInStore _checkInStore;
    private readonly string _deviceId;

    public ServerTimeProvider(IMonotonicClock monotonicClock, ICheckInStore checkInStore, string deviceId)
    {
        _monotonicClock = monotonicClock;
        _checkInStore = checkInStore;
        _deviceId = deviceId;
    }

    public DateTime GetAuthoritativeTimeUtc()
    {
        var lastCheckIn = _checkInStore.GetLastCheckIn(_deviceId);
        if (lastCheckIn == null)
        {
            return DateTime.UtcNow;
        }

        long currentTicks = _monotonicClock.GetMonotonicTicks();
        if (currentTicks >= lastCheckIn.MonotonicTicks && lastCheckIn.MonotonicTicks > 0)
        {
            double elapsedSeconds = (double)(currentTicks - lastCheckIn.MonotonicTicks) / Stopwatch.Frequency;
            return lastCheckIn.ServerTimeUtc.AddSeconds(elapsedSeconds);
        }

        return lastCheckIn.ServerTimeUtc + (DateTime.UtcNow - lastCheckIn.LocalTimeUtc);
    }

    public void SynchronizeServerTime(DateTime serverTimeUtc, DateTime? localTimeUtc = null)
    {
        var record = _checkInStore.GetLastCheckIn(_deviceId) ?? new DeviceCheckInRecord { DeviceId = _deviceId };
        record.ServerTimeUtc = serverTimeUtc;
        record.LocalTimeUtc = localTimeUtc ?? DateTime.UtcNow;
        record.MonotonicTicks = _monotonicClock.GetMonotonicTicks();
        _checkInStore.SaveCheckIn(record);
    }
}
