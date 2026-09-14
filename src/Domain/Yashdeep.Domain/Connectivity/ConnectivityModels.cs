namespace Yashdeep.Domain.Connectivity;

/// <summary>
/// Domain record tracking last verified server check-in details.
/// </summary>
public class DeviceCheckInRecord
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime ServerTimeUtc { get; set; }
    public DateTime LocalTimeUtc { get; set; }
    public long MonotonicTicks { get; set; }
    public string ServerVersion { get; set; } = string.Empty;
    public bool IsSuspended { get; set; }
    public bool RequiresUpdate { get; set; }
}

/// <summary>
/// Evaluation context supplied when determining current connectivity state.
/// </summary>
public class ConnectivityEvaluationContext
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime CurrentLocalTimeUtc { get; set; }
    public DateTime? ServerTimeUtc { get; set; }
    public long CurrentMonotonicTicks { get; set; }
    public bool IsOnline { get; set; }
    public bool LastSyncFailed { get; set; }
    public string? SyncFailureReason { get; set; }
    public bool IsDeviceSuspended { get; set; }
    public bool IsUpdateRequired { get; set; }
}

/// <summary>
/// Result returned after evaluating connectivity policy against current device state.
/// </summary>
public class ConnectivityStateResult
{
    public ConnectivityState State { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan ElapsedSinceLastCheckIn { get; set; }
    public TimeSpan RemainingCheckInTime { get; set; }
    public TimeSpan RemainingGraceTime { get; set; }
    public bool IsClockTampered { get; set; }
    public bool CanPerformTransactions { get; set; }
    public DateTime LastVerifiedServerTimeUtc { get; set; }
}
