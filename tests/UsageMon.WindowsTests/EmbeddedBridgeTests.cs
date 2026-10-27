using System.Diagnostics;
using System.IO;
using UsageMon.App;
using UsageMon.Core;

namespace UsageMon.WindowsTests;

public class EmbeddedBridgeTests
{
    [Fact]
    public async Task EmbeddedHelperCanBeInstalledAndRunWithoutExternalScriptFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "usagemon embedded " + Guid.NewGuid());
        var data = Path.Combine(root, "UsageMon");
        try
        {
            var result = ClaudeSetup.Configure(Path.Combine(root, ".claude", "settings.json"), data, ClaudeBridge.Content);
            Assert.True(result.Configured);
            var info = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(data, "claude-statusline.ps1") }) info.ArgumentList.Add(arg);
            info.Environment["LOCALAPPDATA"] = root;
            using var process = Process.Start(info)!;
            try
            {
                await process.StandardInput.WriteLineAsync("""{"rate_limits":{"five_hour":{"used_percentage":25,"resets_at":1800000200}}}""");
                process.StandardInput.Close();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await process.WaitForExitAsync(timeout.Token);
                Assert.Equal(0, process.ExitCode);
                Assert.Contains("5h 25%", await output);
                Assert.Empty(await error);
                var snapshot = await ClaudeSnapshotReader.ReadAsync(Path.Combine(data, "claude-usage.json"));
                Assert.Equal(25, snapshot.FiveHour!.UsedPercent);
                Assert.Null(snapshot.Weekly);
            }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
