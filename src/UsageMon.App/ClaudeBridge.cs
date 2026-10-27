namespace UsageMon.App;

public static class ClaudeBridge
{
    public static string Content
    {
        get
        {
            using var stream = typeof(ClaudeBridge).Assembly.GetManifestResourceStream("UsageMon.ClaudeStatusLine.ps1")
                ?? throw new InvalidOperationException("The embedded Claude helper is missing. Download UsageMon again.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    public static string SettingsPath => Path.Combine(
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } directory
            ? directory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"), "settings.json");
}
