using System;

namespace Yashdeep.Shared.Connectivity;

/// <summary>
/// Explicitly defined 8 operational connectivity states for edge POS terminals.
/// </summary>
public enum ConnectivityState
{
    /// <summary>
    /// 1. Normal offline operation: Terminal is offline but within valid 7-day (168-hour) window. Full POS enabled.
    /// </summary>
    NormalOfflineActive = 1,

    /// <summary>
    /// 2. Connectivity warning: Offline for 5 to 7 days (120 to 168 hours). Full POS enabled, UI banner warning visible.
    /// </summary>
    ConnectivityWarning = 2,

    /// <summary>
    /// 3. Connectivity expiry: Offline duration exceeds 7 days (168 hours). Expiration evaluation pending.
    /// </summary>
    ConnectivityExpiry = 3,

    /// <summary>
    /// 4. Restricted operation: Connectivity window and soft grace period fully expired. Read-only lockout mode.
    /// </summary>
    RestrictedOperation = 4,

    /// <summary>
    /// 5. Synchronization failure: Outbox push encountered network drops or transient errors. POS operational, background retry active.
    /// </summary>
    SynchronizationFailure = 5,

    /// <summary>
    /// 6. Update requirement: Web API mandates client software version update before transaction continuation.
    /// </summary>
    UpdateRequirement = 6,

    /// <summary>
    /// 7. Device suspension: Device authorization revoked by administrator. Local session cleared.
    /// </summary>
    DeviceSuspension = 7,

    /// <summary>
    /// 8. Recovery: Transition state when online check-in completes after warning, expiry, or failure. Restores normal active status.
    /// </summary>
    Recovery = 8
}

/// <summary>
/// Immutable representation of connectivity status and weekly mandatory check-in metadata.
/// </summary>
public record ConnectivityContext(
    ConnectivityState State,
    DateTime LastVerifiedServerTimeUtc,
    DateTime MonotonicLocalTimeUtc,
    TimeSpan ContinuousOfflineDuration,
    bool ClockTamperingDetected)
{
    /// <summary>
    /// Mandatory 7-day (168-hour) offline window limit.
    /// </summary>
    public static readonly TimeSpan MandatoryOfflineWindow = TimeSpan.FromHours(168);

    /// <summary>
    /// Warning threshold (120 hours / 5 days).
    /// </summary>
    public static readonly TimeSpan WarningThreshold = TimeSpan.FromHours(120);

    /// <summary>
    /// Checks whether the offline duration has breached the 7-day mandatory limit.
    /// </summary>
    public bool IsMandatoryWindowExpired => ContinuousOfflineDuration > MandatoryOfflineWindow;

    /// <summary>
    /// Checks whether POS transactional operations (order entry, checkout) are permitted under current state.
    /// </summary>
    public bool IsTransactionalOperationPermitted =>
        !ClockTamperingDetected &&
        State is ConnectivityState.NormalOfflineActive or ConnectivityState.ConnectivityWarning or ConnectivityState.Recovery;
}
