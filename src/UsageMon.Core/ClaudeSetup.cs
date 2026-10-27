using System.Text.Json;
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
        var original = File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null;
        var originalText = original is null ? null : File.ReadAllText(settingsPath);
        var settings = originalText is null ? new JsonObject() :
            JsonNode.Parse(originalText, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
            ?? throw new JsonException("Claude settings must contain a JSON object.");
        var existing = settings["statusLine"];
        if (existing is not null && !IsUsageMon(existing, dataDirectory)) return new(false, null);

        var line = existing as JsonObject;
        if (line is null) { line = new JsonObject(); settings["statusLine"] = line; }
        line["type"] = "command";
        line["command"] = Command(dataDirectory);
        var updated = settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;

        // Do not overwrite a settings file changed by another program while setup was reading it.
        if (File.Exists(settingsPath) != (original is not null) ||
            (originalText is not null && File.ReadAllText(settingsPath) != originalText))
            throw new IOException("Claude settings changed during setup. Try again.");

        InstallBridge(dataDirectory, bridgeContent);
        if (updated == originalText) return new(true, null);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!);
        string? backup = null;
        if (original is not null)
        {
            backup = settingsPath + ".usagemon-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json";
            File.WriteAllBytes(backup, original);
        }
        AtomicWrite(settingsPath, updated, overwrite: original is not null);
        return new(true, backup);
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
