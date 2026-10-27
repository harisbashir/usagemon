namespace UsageMon.Core;

public sealed class ProviderState
{
    public UsageSnapshot? Snapshot { get; private set; }
    public string? Error { get; private set; }
    public void Update(UsageSnapshot snapshot) { Snapshot = snapshot; Error = null; }
    public void Fail(string message) => Error = message;
    public bool IsStale(DateTimeOffset now) => Error is not null || UsageDisplay.IsStale(Snapshot, now);
}
