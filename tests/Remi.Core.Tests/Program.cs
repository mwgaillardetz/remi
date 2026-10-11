using System.Net;
using System.Text;
using System.Text.Json;
using Remi.Core;

var passed = 0;
await Run("Host validation and normalization", () =>
{
    Equal(Settings.NormalizeHost(""), "http://127.0.0.1:11434");
    Equal(Settings.NormalizeHost(" https://example.com/ollama/ "), "https://example.com/ollama");
    foreach (var bad in new[] { "file:///tmp", "ftp://example.com", "http://user:pass@localhost", "http://localhost?x=1", "bad" })
        Throws<ArgumentException>(() => Settings.NormalizeHost(bad));
    return Task.CompletedTask;
});
await Run("Bounds and invalid appearance settings", () =>
{
    var settings = new Settings { Scale = double.NaN, Fur = "invalid", Outfit = "invalid", SpeechRate = 100, Microphone = -20, Left = double.PositiveInfinity }.Normalize();
    Equal(settings.Scale, 100d); Equal(settings.Fur, "grey"); Equal(settings.Outfit, "none"); Equal(settings.SpeechRate, 10); Equal(settings.Microphone, -1); Equal(settings.Left, null);
    Equal((new Settings { Scale = 500 }).Normalize().Scale, 150d);
    return Task.CompletedTask;
});
await Run("Atomic settings roundtrip and corrupt file preservation", () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "remi-test-" + Guid.NewGuid().ToString("N"));
    var path = Path.Combine(folder, "settings.json");
    try
    {
        var store = new SettingsStore(path);
        Equal(store.Load().Model, "llama3.2");
        var saved = new Settings { Fur = "amber", Outfit = "tuxedo", SpeechEnabled = false, Scale = 125 };
        store.Save(saved); Equal(store.Load(), saved);
        Equal(File.Exists(path + ".tmp"), false);
        File.WriteAllText(path, "broken settings");
        Equal(store.Load(), new Settings()); Equal(File.ReadAllText(path), "broken settings");
        True(store.LoadWarning != null);
    }
    finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    return Task.CompletedTask;
});
await Run("Electron settings migration", () =>
{
    var migrated = SettingsMigration.FromElectronJson("{\"ollamaHost\":\"http://localhost:1234/\",\"model\":\"custom\",\"furColor\":\"amber\",\"showShirt\":true,\"shirtStyle\":\"tuxedo\",\"scale\":125,\"ttsEnabled\":false,\"ttsRate\":2,\"selectedMic\":\"browser-id\",\"showSunglasses\":true}");
    Equal(migrated.Host, "http://localhost:1234"); Equal(migrated.Model, "custom"); Equal(migrated.Outfit, "tuxedo");
    Equal(migrated.Fur, "amber"); Equal(migrated.Scale, 125d); Equal(migrated.SpeechRate, 5); Equal(migrated.Microphone, -1);
    Equal(migrated.SpeechEnabled, false); Equal(migrated.Sunglasses, true);
    Throws<ArgumentException>(() => SettingsMigration.FromElectronJson("[]"));
    return Task.CompletedTask;
});
await Run("Models use configured host and deduplicate", async () =>
{
    using var http = new HttpClient(new Handler((request, _) =>
    {
        Equal(request.RequestUri!.ToString(), "http://localhost:1234/custom/api/tags");
        return Task.FromResult(Response("{\"models\":[{\"name\":\"a\"},{\"name\":\"a\"},{\"name\":\"b\"}]}"));
    }));
    var result = await new OllamaClient(http).ListModelsAsync("http://localhost:1234/custom/");
    Equal(string.Join(',', result), "a,b");
});
await Run("Stream tokens, bound history, preserve order, unload model", async () =>
{
    var history = Enumerable.Range(0, 61).Select(i => new ChatMessage(i % 2 == 0 ? "user" : "assistant", "turn " + i)).ToArray();
    using var http = new HttpClient(new Handler(async (request, token) =>
    {
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
        var root = json.RootElement; var messages = root.GetProperty("messages");
        True(messages.GetArrayLength() <= OllamaClient.HistoryLimit + 1);
        Equal(messages[0].GetProperty("role").GetString(), "system");
        Equal(messages[1].GetProperty("role").GetString(), "user");
        Equal(messages[messages.GetArrayLength() - 1].GetProperty("content").GetString(), "turn 60");
        Equal(root.GetProperty("keep_alive").GetString(), "0");
        Equal(root.GetProperty("stream").GetBoolean(), true);
        return Response("\n{\"message\":{\"content\":\"héllo \"},\"done\":false}\n{\"message\":{\"content\":\"🐨\"},\"done\":true}\n");
    }));
    var result = new StringBuilder();
    await foreach (var chunk in new OllamaClient(http).ChatAsync(new Settings(), history)) result.Append(chunk);
    Equal(result.ToString(), "héllo 🐨");
});
await Run("Optional warm model and single user message", async () =>
{
    using var http = new HttpClient(new Handler(async (request, token) =>
    {
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
        Equal(json.RootElement.GetProperty("keep_alive").GetString(), "5m");
        Equal(json.RootElement.GetProperty("messages").GetArrayLength(), 2);
        return Response("{\"message\":{\"content\":\"ok\"},\"done\":true}\n");
    }));
    await foreach (var _ in new OllamaClient(http).ChatAsync(new Settings { UnloadModel = false }, [new("user", "hello")])) { }
});
await Run("Incomplete stream is rejected", async () =>
{
    using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Response("{\"message\":{\"content\":\"partial\"},\"done\":false}\n"))));
    await ThrowsAsync<IOException>(async () => { await foreach (var _ in new OllamaClient(http).ChatAsync(new Settings(), [new("user", "hello")])) { } });
});
await Run("API error and stream error surface without leaking into messages", async () =>
{
    using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"model missing\"}") })));
    var exception = await ThrowsAsync<HttpRequestException>(async () => await new OllamaClient(http).ListModelsAsync("http://localhost"));
    True(exception.Message.Contains("model missing"));
    using var streamHttp = new HttpClient(new Handler((_, _) => Task.FromResult(Response("{\"error\":\"out of memory\"}\n"))));
    await ThrowsAsync<InvalidOperationException>(async () => { await foreach (var _ in new OllamaClient(streamHttp).ChatAsync(new Settings(), [new("user", "hello")])) { } });
});
await Run("Cancellation interrupts an in-flight HTTP request", async () =>
{
    using var source = new CancellationTokenSource();
    using var http = new HttpClient(new Handler(async (_, token) => { source.Cancel(); await Task.Delay(Timeout.Infinite, token); return Response(""); }));
    await ThrowsAsync<OperationCanceledException>(async () => { await foreach (var _ in new OllamaClient(http).ChatAsync(new Settings(), [new("user", "hello")], source.Token)) { } });
});
await Run("Cancellation interrupts stream reading", async () =>
{
    using var source = new CancellationTokenSource();
    using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new WaitingStream()) })));
    var task = ThrowsAsync<OperationCanceledException>(async () => { await foreach (var _ in new OllamaClient(http).ChatAsync(new Settings(), [new("user", "hello")], source.Token)) { } });
    source.Cancel(); await task;
});
await Run("Invalid conversation is rejected before making a request", async () =>
{
    using var http = new HttpClient(new Handler((_, _) => throw new Exception("Should not make a request")));
    await ThrowsAsync<ArgumentException>(async () => { await foreach (var _ in new OllamaClient(http).ChatAsync(new Settings(), [new("assistant", "hello")])) { } });
});
await Run("Provider, companion, and theme defaults preserve old preferences", () =>
{
    var old = JsonSerializer.Deserialize<Settings>("{\"Fur\":\"amber\"}")!.Normalize();
    Equal(old.CharacterStyle, "professional"); Equal(old.Provider, "ollama"); Equal(old.Theme, "eucalyptus"); Equal(old.Fur, "amber");
    var value = new Settings { Provider = "invalid", CharacterStyle = "invalid", Theme = "invalid", OpenAiModel = " " }.Normalize();
    Equal(value.Provider, "ollama"); Equal(value.CharacterStyle, "professional"); Equal(value.Theme, "eucalyptus"); Equal(value.OpenAiModel, "gpt-4.1-mini");
    Equal((new Settings { CharacterStyle = "robot", Provider = "openai", Theme = "midnight" }).Normalize().CharacterStyle, "robot");
    True(ConversationContext.Instructions(new Settings { CharacterStyle = "cute" }).Contains("playful"));
    True(ConversationContext.Instructions(new Settings()).Contains("capable"));
    return Task.CompletedTask;
});
await Run("Theme colors validate and keep text readable", () =>
{
    Equal(ThemePalette.NormalizeColor(" #aabbcc "), "#AABBCC"); Equal(ThemePalette.NormalizeColor(""), "");
    foreach (var color in new[] { "red", "#FFF", "#12345678", "#ZZZZZZ" }) Throws<ArgumentException>(() => ThemePalette.NormalizeColor(color));
    foreach (var theme in new[] { "eucalyptus", "slate", "midnight" })
    foreach (var background in new[] { "", "#000000", "#FFFFFF", "#777777", "#FF0000", "#00FF00", "#0000FF" })
    foreach (var accent in new[] { "", "#FFFFFF", "#000000", "#888888" })
    {
        var palette = ThemePalette.FromSettings(new Settings { Theme = theme, BackgroundColor = background, AccentColor = accent });
        True(ThemePalette.Contrast(palette.Ink, palette.Paper) >= 4.5);
        True(ThemePalette.Contrast(palette.AccentInk, palette.Accent) >= 4.5);
        True(ThemePalette.Contrast(palette.AccentText, palette.Paper) >= 4.5);
    }
    return Task.CompletedTask;
});
await Run("OpenAI Responses streams Unicode SSE and bounds private context", async () =>
{
    var history = Enumerable.Range(0, 61).Select(i => new ChatMessage(i % 2 == 0 ? "user" : "assistant", "turn " + i)).ToArray();
    using var http = new HttpClient(new Handler(async (request, token) =>
    {
        Equal(request.RequestUri!.ToString(), "https://api.openai.com/v1/responses"); Equal(request.Headers.Authorization!.ToString(), "Bearer fixture-key");
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
        var root = json.RootElement; var input = root.GetProperty("input");
        True(input.GetArrayLength() <= OllamaClient.HistoryLimit); Equal(input[0].GetProperty("role").GetString(), "user");
        Equal(input[input.GetArrayLength() - 1].GetProperty("content").GetString(), "turn 60");
        Equal(root.GetProperty("store").GetBoolean(), false); Equal(root.GetProperty("stream").GetBoolean(), true);
        Equal(root.GetProperty("model").GetString(), "gpt-4.1-mini"); True(root.GetProperty("instructions").GetString()!.Contains("capable"));
        return Response(": heartbeat\r\nevent: response.created\r\ndata: {\"type\":\"response.created\"}\r\n\r\ndata: {\"type\":\"response.output_text.delta\",\r\ndata: \"delta\":\"héllo 🦾\"}\r\n\r\ndata: {\"type\":\"response.completed\"}\r\n\r\n");
    }));
    var text = new StringBuilder(); await foreach (var delta in new OpenAiClient(http).ChatAsync(new Settings(), "fixture-key", history)) text.Append(delta);
    Equal(text.ToString(), "héllo 🦾"); Equal(http.DefaultRequestHeaders.Authorization, null);
});
await Run("OpenAI models and Ollama requests keep credentials separate", async () =>
{
    using var http = new HttpClient(new Handler((request, _) =>
    {
        if (request.RequestUri!.Host == "api.openai.com")
        { Equal(request.Headers.Authorization!.Parameter, "fixture-key"); return Task.FromResult(Response("{\"data\":[{\"id\":\"gpt-4.1-mini\"},{\"id\":\"gpt-4.1-mini\"},{\"id\":\"gpt-realtime\"},{\"id\":\"text-embedding-3-small\"}]}")); }
        Equal(request.Headers.Authorization, null); return Task.FromResult(Response("{\"models\":[]}"));
    }));
    Equal(string.Join(',', await new OpenAiClient(http).ListModelsAsync("fixture-key")), "gpt-4.1-mini");
    await new OllamaClient(http).ListModelsAsync("http://localhost");
});
await Run("OpenAI errors redact credentials and reject interrupted output", async () =>
{
    using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{\"error\":{\"message\":\"Incorrect key fixture-key\"}}") })));
    var error = await ThrowsAsync<HttpRequestException>(async () => await new OpenAiClient(http).ListModelsAsync("fixture-key"));
    True(!error.Message.Contains("fixture-key")); Equal(error.StatusCode, HttpStatusCode.Unauthorized);
    foreach (var type in new[] { "response.failed", "response.incomplete", "error" })
    {
        using var streamHttp = new HttpClient(new Handler((_, _) => Task.FromResult(Response("data: {\"type\":\"" + type + "\"}\n\n"))));
        await ThrowsAsync<InvalidOperationException>(async () => { await foreach (var _ in new OpenAiClient(streamHttp).ChatAsync(new Settings(), "fixture-key", [new("user", "hello")])) { } });
    }
    using var unfinished = new HttpClient(new Handler((_, _) => Task.FromResult(Response("data: {\"type\":\"response.output_text.delta\",\"delta\":\"partial\"}\n\n"))));
    await ThrowsAsync<IOException>(async () => { await foreach (var _ in new OpenAiClient(unfinished).ChatAsync(new Settings(), "fixture-key", [new("user", "hello")])) { } });
});
await Run("OpenAI missing keys fail before network and streams cancel promptly", async () =>
{
    using var http = new HttpClient(new Handler((_, _) => throw new Exception("Should not make a request")));
    await ThrowsAsync<InvalidOperationException>(async () => { await foreach (var _ in new OpenAiClient(http).ChatAsync(new Settings(), "", [new("user", "hello")])) { } });
    using var source = new CancellationTokenSource();
    using var waiting = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new WaitingStream()) })));
    var task = ThrowsAsync<OperationCanceledException>(async () => { await foreach (var _ in new OpenAiClient(waiting).ChatAsync(new Settings(), "fixture-key", [new("user", "hello")], source.Token)) { } });
    source.Cancel(); await task;
});
Console.WriteLine($"{passed} checks passed.");
if (args.Contains("--live"))
{
    var model = args.FirstOrDefault(a => a.StartsWith("--model=", StringComparison.Ordinal))?[8..] ?? "llama3.1:8b";
    using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    var client = new OllamaClient(http);
    var available = await client.ListModelsAsync("http://127.0.0.1:11434");
    True(available.Contains(model));
    var response = new StringBuilder();
    await foreach (var chunk in client.ChatAsync(new Settings { Model = model }, [new("user", "Reply with a short hello in one sentence.")])) response.Append(chunk);
    True(response.Length > 0);
    Console.WriteLine("PASS live local Ollama streaming, with model release requested: " + response);
}

async Task Run(string name, Func<Task> test) { await test(); passed++; Console.WriteLine("PASS " + name); }
static void True(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
static void Equal<T>(T actual, T expected) { if (!Equals(actual, expected)) throw new Exception($"Expected '{expected}', got '{actual}'"); }
static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T exception) { return exception; } throw new Exception("Expected " + typeof(T).Name); }
static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
{ protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => handle(request, token); }

sealed class WaitingStream : Stream
{
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    { await Task.Delay(Timeout.Infinite, cancellationToken); return 0; }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
