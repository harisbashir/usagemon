using System.Text.Json;

namespace UsageMon.Core;

public sealed record WidgetSettings
{
    public double? X { get; init; }
    public double? Y { get; init; }
    public bool Locked { get; init; }
}

public static class SettingsStore
{
    public static WidgetSettings Load(string path)
    {
        try { return JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(path)) ?? new(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public static void Save(string path, WidgetSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
