using System.Net;

static async Task Probe(string name, string url, bool useProxy)
{
    Console.WriteLine($"=== {name} (useProxy={useProxy}) : {url} ===");
    try
    {
        using var handler = new SocketsHttpHandler { UseProxy = useProxy };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        using var resp = await client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        var shown = body.Length > 200 ? body[..200] : body;
        Console.WriteLine($"STATUS {(int)resp.StatusCode} {resp.StatusCode}  body={shown}");
    }
    catch (Exception ex)
    {
        int depth = 0;
        for (var e = ex; e != null && depth < 8; e = e.InnerException, depth++)
            Console.WriteLine($"  EX{depth} [{e.GetType().FullName}]: {e.Message}");
    }
}

Console.WriteLine("DefaultWebProxy = " + WebRequest.DefaultWebProxy?.GetProxy(new Uri("https://api.deepseek.com")));
await Probe("api.deepseek.com", "https://api.deepseek.com/models", useProxy: false);
await Probe("api.deepseek.com", "https://api.deepseek.com/models", useProxy: true);
await Probe("example.com", "https://example.com", useProxy: false);
await Probe("www.baidu.com", "https://www.baidu.com", useProxy: false);
