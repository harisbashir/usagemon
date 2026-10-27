namespace UsageMon.Tests;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var mode = args.FirstOrDefault();
        if (mode == "exit") return;
        if (mode == "hang") { await Task.Delay(Timeout.Infinite); return; }
        var initialized = false;
        var acknowledged = false;
        while (await Console.In.ReadLineAsync() is { } line)
        {
            using var json = System.Text.Json.JsonDocument.Parse(line);
            var root = json.RootElement;
            if (!root.TryGetProperty("method", out _)) continue;
            var method = root.GetProperty("method").GetString();
            if (method == "initialized") { acknowledged = initialized; continue; }
            var id = root.GetProperty("id").GetInt32();
            if (method == "initialize")
            {
                initialized = root.GetProperty("params").GetProperty("clientInfo").GetProperty("name").GetString() == "usagemon";
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { id, result = new { } }));
                continue;
            }
            Console.WriteLine("""{"method":"account/rateLimits/updated","params":{}}""");
            Console.WriteLine("""{"id":"server-notification","method":"unrelated","params":{}}""");
            if (!acknowledged || method != "account/rateLimits/read" || mode == "error")
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { id, error = new { code = -32000, message = "sensitive" } }));
            else
                Console.WriteLine("""{"id":ID,"result":{"rateLimits":{"primary":{"usedPercent":42,"windowDurationMins":300,"resetsAt":1800001000},"secondary":{"usedPercent":77,"windowDurationMins":10080,"resetsAt":1800100000}}}}""".Replace("ID", id.ToString()));
        }
    }
}
