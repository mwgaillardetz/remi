using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Remi.Core;

public sealed record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public sealed class OllamaClient(HttpClient http)
{
    public const int HistoryLimit = 24;

    public async Task<IReadOnlyList<string>> ListModelsAsync(string host, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var response = await http.GetAsync(Settings.NormalizeHost(host) + "/api/tags", timeout.Token);
        await CheckResponseAsync(response, timeout.Token);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
        if (!document.RootElement.TryGetProperty("models", out var models)) return [];
        return models.EnumerateArray().Select(m => m.GetProperty("name").GetString()!)
            .Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToArray();
    }

    public async IAsyncEnumerable<string> ChatAsync(Settings settings, IReadOnlyList<ChatMessage> conversation,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var bounded = ConversationContext.Bound(conversation);
        bounded.Insert(0, new ChatMessage("system", ConversationContext.Instructions(settings)));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var request = new HttpRequestMessage(HttpMethod.Post, Settings.NormalizeHost(settings.Host) + "/api/chat")
        {
            Content = JsonContent.Create(new
            {
                model = settings.Model,
                messages = bounded,
                stream = true,
                keep_alive = settings.UnloadModel ? "0" : "5m",
                options = new { num_ctx = 4096, num_predict = 1024 }
            })
        };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        await CheckResponseAsync(response, timeout.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        while (await reader.ReadLineAsync(timeout.Token) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.GetString());
            if (root.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content)
                && content.GetString() is { Length: > 0 } token) yield return token;
            if (root.TryGetProperty("done", out var done) && done.GetBoolean()) yield break;
        }
        throw new IOException("Ollama closed the response before finishing. Please try again.");
    }

    private static async Task CheckResponseAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(token);
        try
        {
            using var json = JsonDocument.Parse(detail);
            if (json.RootElement.TryGetProperty("error", out var error)) detail = error.GetString() ?? detail;
        }
        catch (JsonException) { }
        if (detail.Length > 400) detail = detail[..400];
        throw new HttpRequestException($"Ollama returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }
}
