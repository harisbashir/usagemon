using System.Globalization;

namespace UsageMon.Core;

public static class UsageDisplay
{
    public static string Percent(UsageWindow? window) => window is null ? "—" :
        window.UsedPercent.ToString("0", CultureInfo.InvariantCulture) + "%";

    public static string Countdown(DateTimeOffset? resetsAt, DateTimeOffset now)
    {
        if (resetsAt is null) return "—";
        var remaining = resetsAt.Value - now;
        if (remaining <= TimeSpan.Zero) return "Updating";
        if (remaining.TotalDays >= 1) return $"{(int)remaining.TotalDays}d {remaining.Hours:00}h";
        if (remaining.TotalHours >= 1) return $"{(int)remaining.TotalHours}h {remaining.Minutes:00}m";
        return $"{remaining.Minutes}m {remaining.Seconds:00}s";
    }

    public static bool IsStale(UsageSnapshot? snapshot, DateTimeOffset now) =>
        snapshot is null || now - snapshot.ObservedAt >= TimeSpan.FromMinutes(5) || snapshot.ObservedAt > now.AddMinutes(1);
}
