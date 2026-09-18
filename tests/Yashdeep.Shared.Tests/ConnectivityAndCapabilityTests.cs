using System;
using Xunit;
using Yashdeep.Shared.Connectivity;
using Yashdeep.Shared.Entitlements;

namespace Yashdeep.Shared.Tests;

public class ConnectivityAndCapabilityTests
{
    [Fact]
    public void ConnectivityContext_NormalOfflineActive_ShouldAllowTransactions()
    {
        var context = new ConnectivityContext(
            State: ConnectivityState.NormalOfflineActive,
            LastVerifiedServerTimeUtc: DateTime.UtcNow.AddDays(-2),
            MonotonicLocalTimeUtc: DateTime.UtcNow,
            ContinuousOfflineDuration: TimeSpan.FromDays(2),
            ClockTamperingDetected: false);

        Assert.True(context.IsTransactionalOperationPermitted);
        Assert.False(context.IsMandatoryWindowExpired);
    }

    [Fact]
    public void ConnectivityContext_Exceeding7Days_ShouldMarkWindowExpired()
    {
        var context = new ConnectivityContext(
            State: ConnectivityState.ConnectivityExpiry,
            LastVerifiedServerTimeUtc: DateTime.UtcNow.AddDays(-8),
            MonotonicLocalTimeUtc: DateTime.UtcNow,
            ContinuousOfflineDuration: TimeSpan.FromDays(8),
            ClockTamperingDetected: false);

        Assert.True(context.IsMandatoryWindowExpired);
        Assert.False(context.IsTransactionalOperationPermitted);
    }

    [Fact]
    public void ConnectivityContext_ClockTampering_ShouldBlockTransactions()
    {
        var context = new ConnectivityContext(
            State: ConnectivityState.NormalOfflineActive,
            LastVerifiedServerTimeUtc: DateTime.UtcNow.AddHours(-10),
            MonotonicLocalTimeUtc: DateTime.UtcNow,
            ContinuousOfflineDuration: TimeSpan.FromHours(10),
            ClockTamperingDetected: true);

        Assert.False(context.IsTransactionalOperationPermitted);
    }

    [Fact]
    public void CapabilityIdentifier_Constants_ShouldBeDefined()
    {
        Assert.Equal("Capability.BarRestaurant.KotBot", CapabilityIdentifier.BarRestaurant_KotBot);
        Assert.Equal("Capability.Hotel.GuestCheckInCheckOut", CapabilityIdentifier.Hotel_GuestCheckInCheckOut);
        Assert.Equal("Capability.Hybrid.PostToRoom", CapabilityIdentifier.Hybrid_PostToRoom);
    }
}
