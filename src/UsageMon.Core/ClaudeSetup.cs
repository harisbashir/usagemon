using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace UsageMon.Core;

public sealed record ClaudeSetupResult(bool Configured, string? BackupPath);

/// <summary>Installs the bridge only on an explicit user action, preserving unrelated Claude settings.</summary>
public static class ClaudeSetup
{
    public static string Command(string dataDirectory) =>
        $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(dataDirectory, "claude-statusline.ps1")}\"";

    public static ClaudeSetupResult Configure(string settingsPath, string dataDirectory, string bridgeContent)
    {
        var initial = File.Exists(settingsPath) ? ClaudeSettingsFile.Parse(File.ReadAllText(settingsPath)) : new JsonObject();
        if (initial["statusLine"] is { } existing && !IsUsageMon(existing, dataDirectory)) return new(false, null);
        InstallBridge(dataDirectory, bridgeContent);
        // Re-read after installing the helper, then hold the settings lock through backup and commit.
        return ClaudeSettingsFile.Update(settingsPath, settings =>
        {
            var current = settings["statusLine"];
            if (current is not null && !IsUsageMon(current, dataDirectory)) return false;
            var line = current as JsonObject;
            if (line is null) { line = new JsonObject(); settings["statusLine"] = line; }
            line["type"] = "command";
            line["command"] = Command(dataDirectory);
            return true;
        });
    }

    public static void InstallBridge(string dataDirectory, string bridgeContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bridgeContent);
        Directory.CreateDirectory(dataDirectory);
        AtomicWrite(Path.Combine(dataDirectory, "claude-statusline.ps1"), bridgeContent, overwrite: true);
    }

    private static bool IsUsageMon(JsonNode node, string dataDirectory)
    {
        if (node is not JsonObject obj || obj["type"] is not JsonValue type ||
            !type.TryGetValue<string>(out var kind) || kind != "command" ||
            obj["command"] is not JsonValue command || !command.TryGetValue<string>(out var value)) return false;
        if (string.Equals(value, Command(dataDirectory), StringComparison.OrdinalIgnoreCase)) return true;
        var match = Regex.Match(value, "^powershell\\.exe -NoProfile -ExecutionPolicy Bypass -File \"(?<path>[^\"]+[\\\\/]claude-statusline\\.ps1)\"$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success || !File.Exists(match.Groups["path"].Value)) return false;
        using var reader = new StreamReader(match.Groups["path"].Value);
        return reader.ReadLine() == "# Receives Claude Code's documented statusLine JSON on stdin.";
    }

    private static void AtomicWrite(string path, string content, bool overwrite)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, content);
            File.Move(temporary, path, overwrite);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
