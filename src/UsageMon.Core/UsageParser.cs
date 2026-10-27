using System.Text.Json;

namespace UsageMon.Core;

public static class UsageParser
{
    public static UsageSnapshot ParseCodex(string json, DateTimeOffset observedAt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var limits = Property(root, "rateLimits");
        var byId = Property(root, "rateLimitsByLimitId");
        var codex = Property(byId, "codex");
        if (codex.ValueKind == JsonValueKind.Object) limits = codex;
        if (limits.ValueKind != JsonValueKind.Object)
            throw new JsonException("No Codex subscription limits returned. Sign in with codex login.");

        UsageWindow? fiveHour = null, weekly = null;
        foreach (var name in new[] { "primary", "secondary" })
        {
            var window = Property(limits, name);
            var duration = Number(Property(window, "windowDurationMins"));
            var normalized = Window(window, "usedPercent", "resetsAt");
            if (duration == 300) fiveHour = normalized;
            if (duration == 10080) weekly = normalized;
        }
        return new(fiveHour, weekly, observedAt);
    }

    public static UsageSnapshot ParseClaude(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var observedAt = Epoch(Property(root, "observed_at"))
            ?? throw new JsonException("Claude snapshot has no valid observation time.");
        var five = Window(Property(root, "five_hour"), "used_percentage", "resets_at");
        var weekly = Window(Property(root, "seven_day"), "used_percentage", "resets_at");
        if (five is null && weekly is null) throw new JsonException("Claude snapshot contains no valid usage windows.");
        return new(five, weekly, observedAt);
    }

    private static UsageWindow? Window(JsonElement element, string percentageName, string resetName)
    {
        var used = Number(Property(element, percentageName));
        return used is >= 0 and <= 100 ? new(used.Value, Epoch(Property(element, resetName))) : null;
    }

    private static JsonElement Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static double? Number(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number) && double.IsFinite(number) ? number : null;

    private static DateTimeOffset? Epoch(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var value)) return null;
        try { return DateTimeOffset.FromUnixTimeSeconds(value); }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
