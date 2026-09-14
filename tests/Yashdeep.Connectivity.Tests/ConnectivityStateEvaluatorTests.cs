namespace Yashdeep.Connectivity.Tests;

using System;
using Xunit;
using Yashdeep.Application.Connectivity;
using Yashdeep.Domain.Connectivity;
using Yashdeep.Infrastructure.Connectivity;

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
    public void Test_4_ExpiryAtSevenDayBoundary_EntersConnectivityExpiry()
    {
        // Arrange: Last check-in at Day 0, current time at Day 7.1 (170 hours offline)
        var lastCheckInTime = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        _checkInStore.SaveCheckIn(new DeviceCheckInRecord
        {
            DeviceId = TestDeviceId,
            ServerTimeUtc = lastCheckInTime,
            LocalTimeUtc = lastCheckInTime,
            MonotonicTicks = _monotonicClock.GetMonotonicTicks()
        });

        var currentTime = lastCheckInTime.AddDays(7.1);
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
        Assert.Equal(ConnectivityState.ConnectivityExpiry, result.State);
        Assert.True(result.CanPerformTransactions); // Grace period active
        Assert.Equal(TimeSpan.Zero, result.RemainingCheckInTime);
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
