namespace Yashdeep.Connectivity.Tests;

using System;
using System.Diagnostics;
using Xunit;
using Yashdeep.Application.Connectivity;
using Yashdeep.Domain.Connectivity;
using Yashdeep.Infrastructure.Connectivity;
using Yashdeep.Shared.Connectivity;

public class ConnectivityStateEvaluatorTests
{
    private readonly ConnectivityPolicyOptions _policyOptions;
    private readonly InMemoryCheckInStore _checkInStore;
    private readonly ClockTamperDetector _clockTamperDetector;
    private readonly SystemMonotonicClock _monotonicClock;
    private readonly ServerTimeProvider _serverTimeProvider;
    private readonly ConnectivityStateEvaluator _evaluator;

    private const string TestDeviceId = "DEV-TEST-001";

    public ConnectivityStateEvaluatorTests()
    {
        _policyOptions = new ConnectivityPolicyOptions
        {
            MandatoryCheckInWindow = TimeSpan.FromDays(7),  // 168 hours
            WarningThreshold = TimeSpan.FromDays(5),        // 120 hours
            SoftGracePeriod = TimeSpan.FromDays(3),         // Configurable PO grace period (e.g. 3 days)
            MaxAllowedClockSkew = TimeSpan.FromMinutes(5)
        };

        _checkInStore = new InMemoryCheckInStore();
        _clockTamperDetector = new ClockTamperDetector();
        _monotonicClock = new SystemMonotonicClock();
        _serverTimeProvider = new ServerTimeProvider(_monotonicClock, _checkInStore, TestDeviceId);

        _evaluator = new ConnectivityStateEvaluator(
            _policyOptions,
            _checkInStore,
            _clockTamperDetector,
            _serverTimeProvider);
    }

    [Fact]
    public void Test_1_SuccessfulOnlineCheckIn_ReturnsRecoveryAndUpdatesServerTime()
    {
        // Arrange
        var baseTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = baseTime,
            ServerTimeUtc = baseTime,
            CurrentMonotonicTicks = _monotonicClock.GetMonotonicTicks(),
            IsOnline = true,
            LastSyncFailed = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.Recovery, result.State);
        Assert.True(result.CanPerformTransactions);
        Assert.Equal(_policyOptions.MandatoryCheckInWindow, result.RemainingCheckInTime);

        // Verify last check-in store was updated
        var lastCheckIn = _checkInStore.GetLastCheckIn(TestDeviceId);
        Assert.NotNull(lastCheckIn);
        Assert.Equal(baseTime, lastCheckIn.ServerTimeUtc);
    }

    [Fact]
    public void Test_2_OfflineOperationWithinSevenDays_ReturnsNormalOfflineOperation()
    {
        // Arrange: Last check-in at Day 0, current time at Day 3 (72 hours offline)
        var lastCheckInTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = _monotonicClock.GetMonotonicTicks()
        });

        var currentTime = lastCheckInTime.AddDays(3);
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = currentTime,
            CurrentMonotonicTicks = _monotonicClock.GetMonotonicTicks(),
            IsOnline = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.NormalOfflineOperation, result.State);
        Assert.True(result.CanPerformTransactions);
        Assert.Equal(TimeSpan.FromDays(4), result.RemainingCheckInTime);
    }

    [Fact]
    public void Test_3_WarningState_TriggeredBetweenWarningThresholdAndSevenDays()
    {
        // Arrange: Last check-in at Day 0, current time at Day 6 (144 hours offline)
        var lastCheckInTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = _monotonicClock.GetMonotonicTicks()
        });

        var currentTime = lastCheckInTime.AddDays(6);
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = currentTime,
            CurrentMonotonicTicks = _monotonicClock.GetMonotonicTicks(),
            IsOnline = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.ConnectivityWarning, result.State);
        Assert.True(result.CanPerformTransactions);
        Assert.Equal(TimeSpan.FromDays(1), result.RemainingCheckInTime);
    }

    [Fact]
    public void Test_4_Boundary_ImmediatelyBeforeSevenDays_ReturnsConnectivityWarning()
    {
        // Arrange: 6 days, 23 hours, 59 minutes offline (167 hours 59 mins)
        var lastCheckInTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = 100000
        });

        var currentTime = lastCheckInTime.AddDays(6).AddHours(23).AddMinutes(59);
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = currentTime,
            CurrentMonotonicTicks = 100000 + (long)(604740 * Stopwatch.Frequency),
            IsOnline = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.ConnectivityWarning, result.State);
        Assert.True(result.CanPerformTransactions);
        Assert.Equal(TimeSpan.FromMinutes(1), result.RemainingCheckInTime);
    }

    [Fact]
    public void Test_4b_Boundary_ExactlyAtSevenDays_ReturnsConnectivityExpiry()
    {
        // Arrange: Exactly 7.0 days offline (168 hours)
        var lastCheckInTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = 100000
        });

        var currentTime = lastCheckInTime.AddDays(7);
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = currentTime,
            CurrentMonotonicTicks = 100000 + (long)(604800 * Stopwatch.Frequency),
            IsOnline = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.ConnectivityExpiry, result.State);
        Assert.True(result.CanPerformTransactions); // Grace active
        Assert.Equal(TimeSpan.Zero, result.RemainingCheckInTime);
    }

    [Fact]
    public void Test_4c_Boundary_ImmediatelyAfterSevenDays_ReturnsConnectivityExpiry()
    {
        // Arrange: 7 days, 00 hours, 01 minute offline
        var lastCheckInTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = 100000
        });

        var currentTime = lastCheckInTime.AddDays(7).AddMinutes(1);
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = currentTime,
            CurrentMonotonicTicks = 100000 + (long)(604860 * Stopwatch.Frequency),
            IsOnline = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.ConnectivityExpiry, result.State);
        Assert.True(result.CanPerformTransactions);
        Assert.Equal(TimeSpan.Zero, result.RemainingCheckInTime);
    }

    [Fact]
    public void Test_4d_GraceBoundary_ImmediatelyBeforeSoftGraceExpiry_ReturnsConnectivityExpiry()
    {
        // Arrange: 7 days + 3 days soft grace - 1 minute (9 days 23 hours 59 mins)
        var lastCheckInTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = 100000
        });

        var currentTime = lastCheckInTime.AddDays(10).AddMinutes(-1);
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = currentTime,
            CurrentMonotonicTicks = 100000 + (long)(863940 * Stopwatch.Frequency),
            IsOnline = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.ConnectivityExpiry, result.State);
        Assert.True(result.CanPerformTransactions);
        Assert.Equal(TimeSpan.FromMinutes(1), result.RemainingGraceTime);
    }

    [Fact]
    public void Test_4e_GraceBoundary_ImmediatelyAfterSoftGraceExpiry_ReturnsRestrictedOperation()
    {
        // Arrange: 7 days + 3 days soft grace + 1 minute (10 days 00 hours 01 min)
        var lastCheckInTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = 100000
        });

        var currentTime = lastCheckInTime.AddDays(10).AddMinutes(1);
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = currentTime,
            CurrentMonotonicTicks = 100000 + (long)(864060 * Stopwatch.Frequency),
            IsOnline = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.RestrictedOperation, result.State);
        Assert.False(result.CanPerformTransactions);
        Assert.Equal(TimeSpan.Zero, result.RemainingGraceTime);
    }

    [Fact]
    public void Test_5_ClockMovedBackward_TriggersRestrictedOperationAndClockTamperFlag()
    {
        // Arrange: Last check-in at Day 5, current local clock manually rolled back to Day 2
        var lastCheckInTime = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = 100000
        });

        var rolledBackTime = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = rolledBackTime,
            CurrentMonotonicTicks = 110000,
            IsOnline = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.RestrictedOperation, result.State);
        Assert.True(result.IsClockTampered);
        Assert.False(result.CanPerformTransactions);
    }

    [Fact]
    public void Test_5b_ClientClockTamperAttemptToExtendWindow_DetectedAndLocked()
    {
        // Scenario: Terminal was offline for 8 days (expired). User rolls system clock back by 5 days to "Day 3".
        var lastCheckInTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = 100000
        });

        // Current real elapsed time is 8 days (691,200 seconds)
        long monotonicTicksAtDay8 = 100000 + (long)(691200 * Stopwatch.Frequency);

        // User changes local system clock to Day 3 (2 days after check-in)
        var tamperedLocalTime = lastCheckInTime.AddDays(2);

        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = tamperedLocalTime,
            CurrentMonotonicTicks = monotonicTicksAtDay8,
            IsOnline = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.RestrictedOperation, result.State);
        Assert.True(result.IsClockTampered);
        Assert.False(result.CanPerformTransactions);
    }

    [Fact]
    public void Test_5c_LargeClockForwardJump_EvaluatedCorrectly()
    {
        // Scenario: System clock jumped forward by 30 days while hardware tick shows only 1 hour elapsed.
        var lastCheckInTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = 100000
        });

        // 1 hour monotonic tick elapsed
        long ticks1HourLater = 100000 + (long)(3600 * Stopwatch.Frequency);
        // System wall-clock jumped to +30 days
        var jumpedTime = lastCheckInTime.AddDays(30);

        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = jumpedTime,
            CurrentMonotonicTicks = ticks1HourLater,
            IsOnline = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert: Elapsed wall-clock > total allowed window, so enters RestrictedOperation
        Assert.Equal(ConnectivityState.RestrictedOperation, result.State);
        Assert.False(result.CanPerformTransactions);
    }

    [Fact]
    public void Test_5d_DeviceNeverConnected_HandlesNullCheckInSafely()
    {
        // Arrange: Newly installed device with no record in check-in store
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = "DEV-NEW-999",
            CurrentLocalTimeUtc = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc),
            CurrentMonotonicTicks = 500000,
            IsOnline = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.NormalOfflineOperation, result.State);
        Assert.True(result.CanPerformTransactions);
        Assert.Equal(_policyOptions.MandatoryCheckInWindow, result.RemainingCheckInTime);
    }

    [Fact]
    public void Test_6_ServerTimeUpdate_UpdatesCheckInStoreAndAuthoritativeTime()
    {
        // Arrange
        var initialServerTime = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        _serverTimeProvider.SynchronizeServerTime(initialServerTime);

        // Act: Perform authoritative time check after check-in
        var authTime = _serverTimeProvider.GetAuthoritativeTimeUtc();

        // Assert
        var lastCheckIn = _checkInStore.GetLastCheckIn(TestDeviceId);
        Assert.NotNull(lastCheckIn);
        Assert.Equal(initialServerTime, lastCheckIn.ServerTimeUtc);
        Assert.True(authTime >= initialServerTime);
    }

    [Fact]
    public void Test_7_RecoveryAfterReconnect_RestoresActiveStateFromExpiryOrFailure()
    {
        // Arrange: Previously in expiry state at Day 8
        var lastCheckInTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = _monotonicClock.GetMonotonicTicks()
        });

        var reconnectTime = lastCheckInTime.AddDays(8);
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = reconnectTime,
            ServerTimeUtc = reconnectTime,
            CurrentMonotonicTicks = _monotonicClock.GetMonotonicTicks(),
            IsOnline = true,
            LastSyncFailed = false
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.Recovery, result.State);
        Assert.True(result.CanPerformTransactions);

        // Verify state is restored and new check-in recorded
        var updatedCheckIn = _checkInStore.GetLastCheckIn(TestDeviceId);
        Assert.NotNull(updatedCheckIn);
        Assert.Equal(reconnectTime, updatedCheckIn.ServerTimeUtc);
    }

    [Fact]
    public void Test_7b_OnlineSyncFailure_ReturnsSyncFailureAndPermitsLocalTxn()
    {
        // Arrange: Device online but batch sync/checkin endpoint failed
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = DateTime.UtcNow,
            CurrentMonotonicTicks = _monotonicClock.GetMonotonicTicks(),
            IsOnline = true,
            LastSyncFailed = true,
            SyncFailureReason = "HTTP 503 Service Unavailable"
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.SynchronizationFailure, result.State);
        Assert.True(result.CanPerformTransactions); // Local POS operations remain active during transient sync failures
        Assert.Contains("503 Service Unavailable", result.Message);
    }

    [Fact]
    public void Test_8_DeviceSuspension_OverridesAllOtherConnectivityStates()
    {
        // Arrange: Device suspended by cloud admin, even while online and synced
        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = DateTime.UtcNow,
            CurrentMonotonicTicks = _monotonicClock.GetMonotonicTicks(),
            IsOnline = true,
            IsDeviceSuspended = true
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.DeviceSuspension, result.State);
        Assert.False(result.CanPerformTransactions);
    }

    [Fact]
    public void Test_9_UpdateRequiredState_OverridesNormalOfflineOperation()
    {
        // Arrange: Mandatory client update required
        var lastCheckInTime = DateTime.UtcNow.AddDays(-1);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = _monotonicClock.GetMonotonicTicks()
        });

        var context = new ConnectivityEvaluationContext
        {
            DeviceId = TestDeviceId,
            CurrentLocalTimeUtc = DateTime.UtcNow,
            CurrentMonotonicTicks = _monotonicClock.GetMonotonicTicks(),
            IsOnline = false,
            IsUpdateRequired = true
        };

        // Act
        var result = _evaluator.EvaluateState(context);

        // Assert
        Assert.Equal(ConnectivityState.UpdateRequirement, result.State);
        Assert.False(result.CanPerformTransactions);
    }
}
