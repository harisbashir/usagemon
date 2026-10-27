using UsageMon.Core;

namespace UsageMon.Tests;

public class ProviderStateTests
{
    [Fact]
    public void RefreshFailureRetainsLastSnapshotAndMarksItStaleUntilSuccess()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new ProviderState();
        var good = new UsageSnapshot(new UsageWindow(32, now.AddHours(1)), null, now);
        state.Update(good);
        Assert.False(state.IsStale(now));
        state.Fail("Sign in again");
        Assert.Same(good, state.Snapshot);
        Assert.True(state.IsStale(now));
        Assert.Equal("Sign in again", state.Error);
        state.Update(good);
        Assert.Null(state.Error);
        Assert.False(state.IsStale(now));
    }

    [Fact]
    public void SettingsRecoverFromCorruptionAndSavePositionAndLock()
    {
        var directory = Path.Combine(Path.GetTempPath(), "usagemon-" + Guid.NewGuid());
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "broken");
            Assert.Null(SettingsStore.Load(path).X);
            SettingsStore.Save(path, new WidgetSettings { X = -1500, Y = 900, Locked = true });
            var settings = SettingsStore.Load(path);
            Assert.Equal(-1500, settings.X);
            Assert.Equal(900, settings.Y);
            Assert.True(settings.Locked);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void MalformedNormalizedSnapshotCannotReplaceValidUsage()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new ProviderState();
        var good = new UsageSnapshot(new UsageWindow(32, now.AddHours(1)), null, now);
        state.Update(good);
        try { state.Update(UsageParser.ParseClaude("""{"observed_at":1800000000,"five_hour":{"used_percentage":"bad"}}""")); }
        catch (System.Text.Json.JsonException) { state.Fail("Invalid snapshot"); }
        Assert.Same(good, state.Snapshot);
        Assert.True(state.IsStale(now));
    }
}
