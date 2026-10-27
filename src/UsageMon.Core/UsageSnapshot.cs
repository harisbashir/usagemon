namespace UsageMon.Core;

public sealed record UsageWindow(double UsedPercent, DateTimeOffset? ResetsAt);

public sealed record UsageSnapshot(UsageWindow? FiveHour, UsageWindow? Weekly, DateTimeOffset ObservedAt);
