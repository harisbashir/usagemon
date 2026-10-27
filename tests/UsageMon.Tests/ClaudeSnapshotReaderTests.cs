using UsageMon.Core;

namespace UsageMon.Tests;

public class ClaudeSnapshotReaderTests
{
    [Fact]
    public async Task ReadsBridgeFileWithUtf8BomFromWindowsPowerShell()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """{"observed_at":1800000000,"five_hour":{"used_percentage":8}}""", new System.Text.UTF8Encoding(true));
            var snapshot = await ClaudeSnapshotReader.ReadAsync(path);
            Assert.Equal(8, snapshot.FiveHour!.UsedPercent);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task MissingSnapshotIsActionableAndCorruptSnapshotIsRejected()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => ClaudeSnapshotReader.ReadAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "{broken");
            await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => ClaudeSnapshotReader.ReadAsync(path));
        }
        finally { File.Delete(path); }
    }
}
