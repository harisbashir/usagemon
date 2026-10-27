using System.ComponentModel;
using System.Text.Json;
using System.Windows.Threading;
using UsageMon.Core;

namespace UsageMon.App;

public sealed class WidgetViewModel : INotifyPropertyChanged
{
    private readonly CodexClient codexClient = new();
    private readonly WindowsMetrics metrics = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly SemaphoreSlim refresh = new(0, 1);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Task[] polling = [];
    private int tick;
    public ProviderViewModel Claude { get; } = new("Claude");
    public ProviderViewModel Codex { get; } = new("Codex");
    public string Cpu { get; private set; } = "—";
    public string Ram { get; private set; } = "—";
    public event PropertyChangedEventHandler? PropertyChanged;
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UsageMon");

    public void Start()
    {
        timer.Tick += OnTick;
        timer.Start();
        metrics.Sample();
        polling = [PollCodexAsync(), PollClaudeAsync()];
        Render();
    }

    public void Refresh()
    {
        if (refresh.CurrentCount == 0) refresh.Release();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (++tick % 2 == 0)
        {
            var (cpu, ram) = metrics.Sample();
            Cpu = cpu is null ? "—" : $"{cpu:0}%";
            Ram = ram is null ? "—" : $"{ram:0}%";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
        Render();
    }

    private void Render()
    {
        var now = DateTimeOffset.UtcNow;
        Claude.Render(now);
        Codex.Render(now);
    }

    private async Task PollCodexAsync()
    {
        var failures = 0;
        var token = cancellation.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var snapshot = await Task.Run(() => codexClient.ReadUsageAsync(token), token);
                    Codex.State.Update(snapshot);
                    failures = 0;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception error) when (error is IOException or InvalidOperationException or TimeoutException or JsonException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
                {
                    failures = Math.Min(3, failures + 1);
                    Codex.State.Fail(error switch
                    {
                        FileNotFoundException => "Codex CLI not found. Install it, run codex login, then restart UsageMon.",
                        TimeoutException => "Codex timed out. Check your connection and CLI login.",
                        JsonException => "Codex returned unavailable or unrecognized limits. Update the CLI and use a ChatGPT subscription login.",
                        _ => "Cannot read Codex limits. Run codex login with your ChatGPT account. Retrying automatically."
                    });
                }
                Render();
                await refresh.WaitAsync(TimeSpan.FromSeconds(Math.Min(300, 60 * Math.Pow(2, Math.Max(0, failures - 1)))), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private async Task PollClaudeAsync()
    {
        var path = Path.Combine(DataDirectory, "claude-usage.json");
        var token = cancellation.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try { Claude.State.Update(await Task.Run(() => ClaudeSnapshotReader.ReadAsync(path, token), token)); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
                {
                    Claude.State.Fail(error is FileNotFoundException or DirectoryNotFoundException
                        ? "Waiting for Claude Code. Open Connection setup to enable the status-line bridge."
                        : "Cannot read the Claude usage snapshot. Last valid reading retained; check Connection setup.");
                }
                Render();
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async Task StopAsync()
    {
        timer.Stop();
        await cancellation.CancelAsync();
        await Task.WhenAll(polling);
        await codexClient.DisposeAsync();
        cancellation.Dispose();
        refresh.Dispose();
    }
}
