using System.Diagnostics;
using System.Text.Json;

namespace UsageMon.Core;

/// <summary>A serialized, read-only client for the installed Codex app server.</summary>
public sealed class CodexClient : IAsyncDisposable
{
    private readonly ProcessStartInfo? startInfo;
    private readonly TimeSpan timeout;
    private readonly SemaphoreSlim gate = new(1, 1);
    private Process? process;
    private int nextId;
    private bool disposed;

    public CodexClient(ProcessStartInfo? startInfo = null, TimeSpan? requestTimeout = null)
    {
        this.startInfo = startInfo;
        timeout = requestTimeout ?? TimeSpan.FromSeconds(20);
    }

    public async Task<UsageSnapshot> ReadUsageAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            try
            {
                if (process is null || process.HasExited)
                {
                    StopProcess();
                    var info = startInfo ?? DefaultStartInfo();
                    info.UseShellExecute = false;
                    info.CreateNoWindow = true;
                    info.RedirectStandardInput = true;
                    info.RedirectStandardOutput = true;
                    info.RedirectStandardError = true;
                    process = Process.Start(info) ?? throw new IOException("Cannot start Codex.");
                    process.BeginErrorReadLine(); // Drain stderr without retaining credential-bearing diagnostics.
                    await RequestAsync("initialize", new { clientInfo = new { name = "usagemon", title = "UsageMon", version = "0.1.0" } }, deadline.Token);
                    await SendAsync(new { method = "initialized", @params = new { } }, deadline.Token);
                }
                var result = await RequestAsync("account/rateLimits/read", null, deadline.Token);
                return UsageParser.ParseCodex(result, DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                StopProcess();
                throw new TimeoutException("Codex did not respond. Check the CLI and sign in with codex login.");
            }
            catch
            {
                StopProcess();
                throw;
            }
        }
        finally { gate.Release(); }
    }

    private async Task<string> RequestAsync(string method, object? parameters, CancellationToken token)
    {
        var id = ++nextId;
        await SendAsync(new { id, method, @params = parameters }, token);
        while (true)
        {
            var line = await process!.StandardOutput.ReadLineAsync(token)
                ?? throw new IOException("Codex exited. Check your CLI installation and run codex login.");
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.TryGetProperty("method", out _))
            {
                if (root.TryGetProperty("id", out var requestId))
                    await SendAsync(new { id = requestId, error = new { code = -32601, message = "Unsupported method" } }, token);
                continue;
            }
            if (!root.TryGetProperty("id", out var responseId) || responseId.ValueKind != JsonValueKind.Number || !responseId.TryGetInt32(out var receivedId) || receivedId != id)
                continue;
            if (root.TryGetProperty("error", out _))
                throw new InvalidOperationException("Codex could not read subscription limits. Run codex login with your ChatGPT account.");
            if (!root.TryGetProperty("result", out var result)) throw new JsonException("Invalid Codex response.");
            return result.GetRawText();
        }
    }

    private async Task SendAsync(object message, CancellationToken token)
    {
        await process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), token);
        await process.StandardInput.FlushAsync(token);
    }

    private static ProcessStartInfo DefaultStartInfo()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                var folder = directory.Trim('"');
                if (string.IsNullOrWhiteSpace(folder)) continue;
                var executable = Path.Combine(folder, "codex.exe");
                if (File.Exists(executable))
                {
                    var info = new ProcessStartInfo(executable);
                    info.ArgumentList.Add("app-server");
                    return info;
                }
                var shim = Path.Combine(folder, "codex.cmd");
                if (File.Exists(shim))
                    return new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                        $"/d /s /c \"\"{shim}\" app-server\"");
            }
            throw new FileNotFoundException("Codex CLI not found. Install it, run codex login, and restart UsageMon.");
        }
        var unix = new ProcessStartInfo("codex");
        unix.ArgumentList.Add("app-server");
        return unix;
    }

    private void StopProcess()
    {
        if (process is null) return;
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        finally { process.Dispose(); process = null; }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try { disposed = true; StopProcess(); }
        finally { gate.Release(); }
    }
}
