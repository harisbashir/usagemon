using System.Diagnostics;
using UsageMon.Core;

namespace UsageMon.Tests;

public class CodexClientTests
{
    private static ProcessStartInfo Fake(string mode)
    {
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet");
        info.ArgumentList.Add(typeof(Program).Assembly.Location);
        info.ArgumentList.Add(mode);
        return info;
    }

    [Fact]
    public async Task PerformsHandshakeIgnoresNotificationsAndSerializesConcurrentReads()
    {
        await using var client = new CodexClient(Fake("normal"), TimeSpan.FromSeconds(5));
        var results = await Task.WhenAll(client.ReadUsageAsync(), client.ReadUsageAsync());
        Assert.All(results, snapshot => Assert.Equal(42, snapshot.FiveHour!.UsedPercent));
        Assert.All(results, snapshot => Assert.Equal(77, snapshot.Weekly!.UsedPercent));
    }

    [Fact]
    public async Task RpcErrorsBecomeActionableWithoutLeakingRawErrorData()
    {
        await using var client = new CodexClient(Fake("error"), TimeSpan.FromSeconds(5));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReadUsageAsync());
        Assert.Contains("codex login", error.Message);
        Assert.DoesNotContain("sensitive", error.Message);
    }

    [Fact]
    public async Task HungChildTimesOutAndClientCanBeDisposed()
    {
        await using var client = new CodexClient(Fake("hang"), TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAsync<TimeoutException>(() => client.ReadUsageAsync());
    }

    [Fact]
    public async Task CancellationDoesNotWaitForRequestTimeout()
    {
        await using var client = new CodexClient(Fake("hang"), TimeSpan.FromSeconds(30));
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReadUsageAsync(cancel.Token));
    }

    [Fact]
    public async Task UnexpectedExitIsReportedInsteadOfHanging()
    {
        await using var client = new CodexClient(Fake("exit"), TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<IOException>(() => client.ReadUsageAsync());
    }
}
