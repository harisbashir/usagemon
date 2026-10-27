namespace UsageMon.Core;

public static class ClaudeSnapshotReader
{
    public static async Task<UsageSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            4096, FileOptions.Asynchronous);
        if (stream.Length > 65536) throw new IOException("Claude usage snapshot is too large.");
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return UsageParser.ParseClaude(await reader.ReadToEndAsync(cancellationToken));
    }
}
