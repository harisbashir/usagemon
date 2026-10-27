using UsageMon.Core;

namespace UsageMon.Tests;

public class UsageParserTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1800000000);

    [Fact]
    public void CodexMapsWindowsByDurationEvenWhenReversed()
    {
        var data = UsageParser.ParseCodex("""
            {"rateLimits":{"primary":{"usedPercent":70,"windowDurationMins":10080,"resetsAt":1800100000},
            "secondary":{"usedPercent":15.5,"windowDurationMins":300,"resetsAt":1800001000}}}
            """, Now);
        Assert.Equal(15.5, data.FiveHour!.UsedPercent);
        Assert.Equal(70, data.Weekly!.UsedPercent);
        Assert.Equal(1800001000, data.FiveHour.ResetsAt!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public void CodexPrefersCodexBucketAndDoesNotInventFiveHourWindow()
    {
        var data = UsageParser.ParseCodex("""
            {"rateLimits":{"primary":{"usedPercent":99,"windowDurationMins":300}},
            "rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":20,"windowDurationMins":10080,"resetsAt":null},"secondary":null}}}
            """, Now);
        Assert.Null(data.FiveHour);
        Assert.Equal(20, data.Weekly!.UsedPercent);
        Assert.Null(data.Weekly.ResetsAt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidUtilizationIsUnavailable(double value)
    {
        var json = """{"rateLimits":{"primary":{"usedPercent":VALUE,"windowDurationMins":300}}}""";
        var data = UsageParser.ParseCodex(json.Replace("VALUE", value.ToString(System.Globalization.CultureInfo.InvariantCulture)), Now);
        Assert.Null(data.FiveHour);
    }

    [Fact]
    public void UnknownDurationIsNotMislabelled()
    {
        var data = UsageParser.ParseCodex("""{"rateLimits":{"primary":{"usedPercent":20,"windowDurationMins":15}}}""", Now);
        Assert.Null(data.FiveHour);
        Assert.Null(data.Weekly);
    }

    [Fact]
    public void ClaudeReadsNormalizedSnapshotAndObservationTime()
    {
        var data = UsageParser.ParseClaude("""
            {"observed_at":1800000000,"five_hour":{"used_percentage":0,"resets_at":1800000100},
            "seven_day":{"used_percentage":82,"resets_at":1800100000}}
            """);
        Assert.Equal(0, data.FiveHour!.UsedPercent);
        Assert.Equal(82, data.Weekly!.UsedPercent);
        Assert.Equal(Now, data.ObservedAt);
    }

    [Fact]
    public void ClaudeMissingObservationTimeIsRejected()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => UsageParser.ParseClaude("{}"));
    }

    [Fact]
    public void OutOfRangeResetDoesNotCrashOrLosePercentage()
    {
        var data = UsageParser.ParseClaude("""{"observed_at":1800000000,"five_hour":{"used_percentage":12,"resets_at":9223372036854775807}}""");
        Assert.Equal(12, data.FiveHour!.UsedPercent);
        Assert.Null(data.FiveHour.ResetsAt);
    }
}
