using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UsageMon.Core;

/// <summary>Edits Claude settings under an exclusive Windows file lock, with an exact backup.</summary>
public static class ClaudeSettingsFile
{
    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static JsonObject Parse(string text)
    {
        using var document = JsonDocument.Parse(text, Options);
        Validate(document.RootElement);
        return JsonNode.Parse(text, documentOptions: Options) as JsonObject
            ?? throw new JsonException("Claude settings must contain a JSON object.");
    }

    public static ClaudeSetupResult Update(string path, Func<JsonObject, bool> edit)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var created = false;
        FileStream file;
        try { file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
        catch (FileNotFoundException)
        {
            // Refuse to replace a file another program creates between Open and CreateNew.
            file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            created = true;
        }
        using (file)
        {
            using var memory = new MemoryStream();
            file.CopyTo(memory);
            var original = memory.ToArray();
            string originalText;
            using (var reader = new StreamReader(new MemoryStream(original))) originalText = reader.ReadToEnd();
            var settings = created ? new JsonObject() : Parse(originalText);
            if (!edit(settings)) return new(false, null);
            var updated = Encoding.UTF8.GetBytes(settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
            if (original.AsSpan().SequenceEqual(updated)) return new(true, null);
            string? backup = null;
            if (!created)
            {
                backup = path + ".usagemon-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json";
                File.WriteAllBytes(backup, original);
            }
            // Keep the same exclusive handle through the commit: on Windows it blocks both
            // in-place writers and renames, which an unlocked atomic replacement cannot do.
            // The backup remains available across a crash; failed writes roll back in-process.
            try { Write(file, updated); }
            catch
            {
                try { Write(file, original); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { throw new IOException($"Claude settings could not be restored. The backup is at {backup ?? path}.", error); }
                throw;
            }
            return new(true, backup);
        }
    }

    private static void Write(FileStream file, byte[] bytes)
    {
        file.Position = 0;
        file.Write(bytes);
        file.SetLength(bytes.Length);
        file.Flush(flushToDisk: true);
    }

    private static void Validate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw new JsonException("Claude settings contain duplicate JSON properties.");
                Validate(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) Validate(item);
    }
}
