using System;
using Xunit;
using Yashdeep.Shared.Time;

namespace Yashdeep.Shared.Tests;

public class DateTimeProviderTests
{
    [Fact]
    public void TestDateTimeProvider_ShouldBeDeterministic()
    {
        var fixedTime = new DateTime(2026, 9, 14, 1, 15, 0, DateTimeKind.Utc);
        var provider = new TestDateTimeProvider(fixedTime);

        Assert.Equal(fixedTime, provider.UtcNow);
        Assert.Equal(fixedTime, provider.UtcNow);
    }

    [Fact]
    public void TestDateTimeProvider_Advance_ShouldIncreaseTimestamp()
    {
        var initial = new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc);
        var provider = new TestDateTimeProvider(initial);

        provider.Advance(TimeSpan.FromHours(2));

        Assert.Equal(new DateTime(2026, 9, 14, 2, 0, 0, DateTimeKind.Utc), provider.UtcNow);
    }

    [Fact]
    public void SystemDateTimeProvider_ShouldReturnUtc()
    {
        var provider = SystemDateTimeProvider.Instance;
        Assert.Equal(DateTimeKind.Utc, provider.UtcNow.Kind);
    }
}
