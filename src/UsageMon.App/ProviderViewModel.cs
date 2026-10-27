using System.ComponentModel;
using System.Windows.Media;
using UsageMon.Core;

namespace UsageMon.App;

public sealed class ProviderViewModel(string name) : INotifyPropertyChanged
{
    public ProviderState State { get; } = new();
    public string Name { get; } = name;
    public string Status { get; private set; } = "connecting";
    public string FivePercent { get; private set; } = "—";
    public string WeekPercent { get; private set; } = "—";
    public string FiveReset { get; private set; } = "—";
    public string WeekReset { get; private set; } = "—";
    public Brush StatusBrush { get; private set; } = Brushes.SlateGray;
    public Brush FiveBrush { get; private set; } = Brushes.White;
    public Brush WeekBrush { get; private set; } = Brushes.White;
    public string Tooltip { get; private set; } = "Reading subscription usage…";
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Render(DateTimeOffset now)
    {
        var snapshot = State.Snapshot;
        var stale = State.IsStale(now);
        Status = snapshot is null ? (State.Error is null ? "connecting" : "setup") : stale ? "stale" : "";
        StatusBrush = snapshot is null ? Brushes.SlateGray : stale ? Brushes.Goldenrod : Brushes.MediumAquamarine;
        FivePercent = UsageDisplay.Percent(snapshot?.FiveHour);
        WeekPercent = UsageDisplay.Percent(snapshot?.Weekly);
        FiveReset = UsageDisplay.Countdown(snapshot?.FiveHour?.ResetsAt, now);
        WeekReset = UsageDisplay.Countdown(snapshot?.Weekly?.ResetsAt, now);
        FiveBrush = UsageBrush(snapshot?.FiveHour);
        WeekBrush = UsageBrush(snapshot?.Weekly);
        var observed = snapshot is null ? "No usage received yet." : $"Last updated: {snapshot.ObservedAt.LocalDateTime:ddd, MMM d HH:mm:ss}";
        Tooltip = $"{Name} · percentage used\n{State.Error ?? (stale ? "Reading is stale; waiting for an update." : "Subscription limits supplied by the provider.")}\n{observed}\n" +
            $"5-hour reset: {ResetDate(snapshot?.FiveHour)}\nWeekly reset: {ResetDate(snapshot?.Weekly)}\nUnavailable windows are shown as —. Right-click for setup.";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private static string ResetDate(UsageWindow? window) => window?.ResetsAt?.LocalDateTime.ToString("ddd, MMM d yyyy HH:mm:ss") ?? "not reported";
    private static Brush UsageBrush(UsageWindow? window) => window?.UsedPercent switch
    {
        >= 90 => Brushes.Salmon,
        >= 70 => Brushes.Goldenrod,
        _ => Brushes.WhiteSmoke
    };
}
