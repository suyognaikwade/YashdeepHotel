namespace Yashdeep.Infrastructure.Connectivity;

using Yashdeep.Application.Connectivity;
using Yashdeep.Domain.Connectivity;
using Yashdeep.Shared.Connectivity;

/// <summary>
/// Evaluates device connectivity state according to mandatory product rules, configurable warning thresholds, and configurable soft grace periods.
/// Handles priority overrides: DeviceSuspension > UpdateRequirement > ClockTampering > Recovery > Expiry/Restricted > Warning > NormalOffline.
/// </summary>
public class ConnectivityStateEvaluator : IConnectivityStateEvaluator
{
    private readonly ConnectivityPolicyOptions _options;
    private readonly ICheckInStore _checkInStore;
    private readonly IClockTamperDetector _clockTamperDetector;
    private readonly IServerTimeProvider _serverTimeProvider;

    public ConnectivityStateEvaluator(
        ConnectivityPolicyOptions options,
        ICheckInStore checkInStore,
        IClockTamperDetector clockTamperDetector,
        IServerTimeProvider serverTimeProvider)
    {
        _options = options;
        _checkInStore = checkInStore;
        _clockTamperDetector = clockTamperDetector;
        _serverTimeProvider = serverTimeProvider;
    }

    public ConnectivityStateResult EvaluateState(ConnectivityEvaluationContext context)
    {
        // 1. Highest Priority Override: Device Suspension
        if (context.IsDeviceSuspended)
        {
            return new ConnectivityStateResult
            {
                State = ConnectivityState.DeviceSuspension,
                Message = "Device authorization has been suspended by administrator.",
                CanPerformTransactions = false
            };
        }

        // 2. Second Priority Override: Update Requirement
        if (context.IsUpdateRequired)
        {
            return new ConnectivityStateResult
            {
                State = ConnectivityState.UpdateRequirement,
                Message = "Client software update is required prior to continuing transactions.",
                CanPerformTransactions = false
            };
        }

        var lastCheckIn = _checkInStore.GetLastCheckIn(context.DeviceId);

        // 3. Clock Tamper Detection
        bool isClockTampered = _clockTamperDetector.IsClockTampered(
            context.CurrentLocalTimeUtc,
            context.CurrentMonotonicTicks,
            lastCheckIn,
            _options.MaxAllowedClockSkew);

        if (isClockTampered)
        {
            return new ConnectivityStateResult
            {
                State = ConnectivityState.RestrictedOperation,
                Message = "System clock tampering or backward rollback detected. Transactions locked.",
                IsClockTampered = true,
                CanPerformTransactions = false
            };
        }

        // 4. Online Check-in / Recovery evaluation
        if (context.IsOnline)
        {
            if (context.LastSyncFailed)
            {
                return new ConnectivityStateResult
                {
                    State = ConnectivityState.SynchronizationFailure,
                    Message = $"Online check-in attempted but sync failed: {context.SyncFailureReason}",
                    CanPerformTransactions = true,
                    LastVerifiedServerTimeUtc = lastCheckIn?.ServerTimeUtc ?? context.CurrentLocalTimeUtc
                };
            }

            // Successful check-in / reconnect
            var verifiedServerTime = context.ServerTimeUtc ?? context.CurrentLocalTimeUtc;
            _serverTimeProvider.SynchronizeServerTime(verifiedServerTime, context.CurrentLocalTimeUtc);

            return new ConnectivityStateResult
            {
                State = ConnectivityState.Recovery,
                Message = "Online check-in successful. Device state restored to active.",
                RemainingCheckInTime = _options.MandatoryCheckInWindow,
                RemainingGraceTime = _options.SoftGracePeriod,
                CanPerformTransactions = true,
                LastVerifiedServerTimeUtc = verifiedServerTime
            };
        }

        // 5. Offline Operation Evaluation
        if (lastCheckIn == null)
        {
            // Initial un-synced setup fallback: treat as normal offline if within window
            return new ConnectivityStateResult
            {
                State = ConnectivityState.NormalOfflineOperation,
                Message = "Initial device setup; operating in normal offline mode.",
                RemainingCheckInTime = _options.MandatoryCheckInWindow,
                RemainingGraceTime = _options.SoftGracePeriod,
                CanPerformTransactions = true,
                LastVerifiedServerTimeUtc = context.CurrentLocalTimeUtc
            };
        }

        TimeSpan elapsed = context.CurrentLocalTimeUtc - lastCheckIn.LocalTimeUtc;
        TimeSpan mandatoryWindow = _options.MandatoryCheckInWindow;
        TimeSpan warningThreshold = _options.WarningThreshold;
        TimeSpan softGracePeriod = _options.SoftGracePeriod;

        TimeSpan remainingCheckInTime = mandatoryWindow > elapsed ? mandatoryWindow - elapsed : TimeSpan.Zero;
        TimeSpan totalAllowedTime = mandatoryWindow + softGracePeriod;
        TimeSpan remainingGraceTime = totalAllowedTime > elapsed ? totalAllowedTime - elapsed : TimeSpan.Zero;

        // Check if beyond mandatory window + grace period (Restricted Operation Lockout)
        if (elapsed > totalAllowedTime)
        {
            return new ConnectivityStateResult
            {
                State = ConnectivityState.RestrictedOperation,
                Message = $"Mandatory connectivity check and soft grace period expired ({elapsed.TotalDays:F1} days offline). Terminal in read-only mode.",
                ElapsedSinceLastCheckIn = elapsed,
                RemainingCheckInTime = TimeSpan.Zero,
                RemainingGraceTime = TimeSpan.Zero,
                CanPerformTransactions = false,
                LastVerifiedServerTimeUtc = lastCheckIn.ServerTimeUtc
            };
        }

        // Check if beyond mandatory 7-day boundary but within soft grace period (Connectivity Expiry)
        if (elapsed >= mandatoryWindow)
        {
            return new ConnectivityStateResult
            {
                State = ConnectivityState.ConnectivityExpiry,
                Message = $"Mandatory 7-day connectivity check-in expired ({elapsed.TotalDays:F1} days offline). Grace period active.",
                ElapsedSinceLastCheckIn = elapsed,
                RemainingCheckInTime = TimeSpan.Zero,
                RemainingGraceTime = remainingGraceTime,
                CanPerformTransactions = true,
                LastVerifiedServerTimeUtc = lastCheckIn.ServerTimeUtc
            };
        }

        // Check if beyond warning threshold (e.g. 5 days / 120 hours) (Connectivity Warning)
        if (elapsed >= warningThreshold)
        {
            return new ConnectivityStateResult
            {
                State = ConnectivityState.ConnectivityWarning,
                Message = $"Device offline for {elapsed.TotalDays:F1} days. Connect to network before 7-day limit expires.",
                ElapsedSinceLastCheckIn = elapsed,
                RemainingCheckInTime = remainingCheckInTime,
                RemainingGraceTime = softGracePeriod,
                CanPerformTransactions = true,
                LastVerifiedServerTimeUtc = lastCheckIn.ServerTimeUtc
            };
        }

        // Normal Offline Operation (0 to 5 days)
        return new ConnectivityStateResult
        {
            State = ConnectivityState.NormalOfflineOperation,
            Message = "Operating in normal offline mode within valid weekly window.",
            ElapsedSinceLastCheckIn = elapsed,
            RemainingCheckInTime = remainingCheckInTime,
            RemainingGraceTime = softGracePeriod,
            CanPerformTransactions = true,
            LastVerifiedServerTimeUtc = lastCheckIn.ServerTimeUtc
        };
    }
}
