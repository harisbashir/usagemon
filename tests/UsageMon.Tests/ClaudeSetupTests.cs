using System.Text.Json;
using UsageMon.Core;

namespace UsageMon.Tests;

public class ClaudeSetupTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "usagemon-setup-" + Guid.NewGuid());
    private string Settings => Path.Combine(root, ".claude", "settings.json");
    private string Data => Path.Combine(root, "local app data", "UsageMon");

    [Fact]
    public void CreatesConfigurationAndInstallsBridgeAtStablePath()
    {
        var result = ClaudeSetup.Configure(Settings, Data, "bridge-content");
        using var json = JsonDocument.Parse(File.ReadAllText(Settings));
        Assert.Equal("command", json.RootElement.GetProperty("statusLine").GetProperty("type").GetString());
        Assert.Equal($"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(Data, "claude-statusline.ps1")}\"",
            json.RootElement.GetProperty("statusLine").GetProperty("command").GetString());
        Assert.Equal("bridge-content", File.ReadAllText(Path.Combine(Data, "claude-statusline.ps1")));
        Assert.Null(result.BackupPath);
        Assert.True(result.Configured);
    }

    [Fact]
    public void PreservesUnrelatedSettingsAndBacksUpExactOriginalBytes()
    {
        const string original = "{\n  \"model\": \"test-model\", \"permissions\": { \"deny\": [\"Read(secret)\"] }\n}";
        WriteSettings(original);
        var result = ClaudeSetup.Configure(Settings, Data, "bridge");
        Assert.Equal(original, File.ReadAllText(result.BackupPath!));
        using var json = JsonDocument.Parse(File.ReadAllText(Settings));
        Assert.Equal("test-model", json.RootElement.GetProperty("model").GetString());
        Assert.Equal("Read(secret)", json.RootElement.GetProperty("permissions").GetProperty("deny")[0].GetString());
    }

    [Fact]
    public void ExistingCustomStatusLineIsNeverOverwritten()
    {
        const string original = """{"statusLine":{"type":"command","command":"my-custom-renderer","padding":2},"model":"keep"}""";
        WriteSettings(original);
        var result = ClaudeSetup.Configure(Settings, Data, "bridge");
        Assert.False(result.Configured);
        Assert.Null(result.BackupPath);
        Assert.Equal(original, File.ReadAllText(Settings));
        Assert.False(Directory.Exists(Data));
    }

    [Fact]
    public void RepeatedSetupUpdatesBridgeAndPreservesStatusLineOptions()
    {
        ClaudeSetup.Configure(Settings, Data, "old");
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Settings))!;
        node["statusLine"]!["padding"] = 3;
        File.WriteAllText(Settings, node.ToJsonString());
        Assert.True(ClaudeSetup.Configure(Settings, Data, "new").Configured);
        Assert.Equal("new", File.ReadAllText(Path.Combine(Data, "claude-statusline.ps1")));
        using var json = JsonDocument.Parse(File.ReadAllText(Settings));
        Assert.Equal(3, json.RootElement.GetProperty("statusLine").GetProperty("padding").GetInt32());
    }

    [Fact]
    public void UpgradesPreviouslyInstalledUsageMonCommand()
    {
        var legacy = Path.Combine(root, "Old Folder", "scripts", "claude-statusline.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, "# Receives Claude Code's documented statusLine JSON on stdin.\n# old bridge version");
        var command = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{legacy}\"";
        WriteSettings(JsonSerializer.Serialize(new { statusLine = new { type = "command", command, padding = 2 } }));
        Assert.True(ClaudeSetup.Configure(Settings, Data, "new").Configured);
        using var json = JsonDocument.Parse(File.ReadAllText(Settings));
        Assert.Contains(Data, json.RootElement.GetProperty("statusLine").GetProperty("command").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("statusLine").GetProperty("padding").GetInt32());
    }

    [Fact]
    public void CustomScriptWithSameFilenameIsStillProtected()
    {
        var custom = Path.Combine(root, "custom", "claude-statusline.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(custom)!);
        File.WriteAllText(custom, "Write-Output 'my custom status line'");
        WriteSettings(JsonSerializer.Serialize(new { statusLine = new { type = "command", command = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{custom}\"" } }));
        var original = File.ReadAllText(Settings);
        Assert.False(ClaudeSetup.Configure(Settings, Data, "bridge").Configured);
        Assert.Equal(original, File.ReadAllText(Settings));
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"model\":\"a\",\"model\":\"b\"}")]
    [InlineData("{\"permissions\":{\"deny\":[],\"deny\":[\"Read(secret)\"]}}")]
    public void InvalidSettingsAreLeftUntouched(string original)
    {
        WriteSettings(original);
        Assert.ThrowsAny<JsonException>(() => ClaudeSetup.Configure(Settings, Data, "bridge"));
        Assert.Equal(original, File.ReadAllText(Settings));
        Assert.False(Directory.Exists(Data));
    }

    [Fact]
    public void SettingsTransactionExcludesConcurrentWritersAndReadsLatestSettings()
    {
        WriteSettings("""{"model":"latest","permissions":{"deny":["Read(secret)"]}}""");
        var result = ClaudeSettingsFile.Update(Settings, settings =>
        {
            Assert.Throws<IOException>(() => File.WriteAllText(Settings, "concurrent overwrite"));
            if (OperatingSystem.IsWindows())
            {
                var replacement = Path.Combine(root, "replacement.json");
                File.WriteAllText(replacement, "concurrent atomic replacement");
                Assert.Throws<IOException>(() => File.Move(replacement, Settings, overwrite: true));
            }
            settings["statusLine"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "command", ["command"] = "our bridge" };
            return true;
        });
        Assert.True(result.Configured);
        using var backup = JsonDocument.Parse(File.ReadAllText(result.BackupPath!));
        Assert.Equal("latest", backup.RootElement.GetProperty("model").GetString());
        Assert.Equal("Read(secret)", backup.RootElement.GetProperty("permissions").GetProperty("deny")[0].GetString());
    }

    private void WriteSettings(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Settings)!);
        File.WriteAllText(Settings, content);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
