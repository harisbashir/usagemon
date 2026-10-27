using UsageMon.Core;

namespace UsageMon.Tests;

public class UsageDisplayTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1800000000);

    [Theory]
    [InlineData(3660, "1h 01m")]
    [InlineData(60, "1m 00s")]
    [InlineData(1, "0m 01s")]
    [InlineData(90000, "1d 01h")]
    [InlineData(0, "Updating")]
    [InlineData(-1, "Updating")]
    public void CountdownUsesResetWithoutInventingNewWindow(int seconds, string expected)
    {
        Assert.Equal(expected, UsageDisplay.Countdown(Now.AddSeconds(seconds), Now));
    }

    [Fact]
    public void UnavailableUsageAndUnknownResetAreDistinct()
    {
        Assert.Equal("—", UsageDisplay.Percent(null));
        Assert.Equal("0%", UsageDisplay.Percent(new UsageWindow(0, null)));
        Assert.Equal("—", UsageDisplay.Countdown(null, Now));
    }

    [Fact]
    public void StaleBoundaryIsFiveMinutesAndFutureTimeIsNotFresh()
    {
        Assert.False(UsageDisplay.IsStale(new UsageSnapshot(null, null, Now.AddSeconds(-299)), Now));
        Assert.True(UsageDisplay.IsStale(new UsageSnapshot(null, null, Now.AddMinutes(-5)), Now));
        Assert.True(UsageDisplay.IsStale(new UsageSnapshot(null, null, Now.AddMinutes(2)), Now));
        Assert.True(UsageDisplay.IsStale(null, Now));
    }
}
