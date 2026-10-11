using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Remi.Core;

/// <summary>Direct Responses API streaming. Credentials are scoped to each OpenAI request.</summary>
public sealed class OpenAiClient(HttpClient http)
{
    private static HttpRequestMessage Request(HttpMethod method, string path, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Add an OpenAI API key in Settings → Connection to use ChatGPT models.");
        if (apiKey.Any(char.IsWhiteSpace)) throw new ArgumentException("The API key must not contain whitespace.");
        var request = new HttpRequestMessage(method, "https://api.openai.com/v1/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(string apiKey, CancellationToken token = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = Request(HttpMethod.Get, "models", apiKey);
        using var response = await http.SendAsync(request, timeout.Token);
        await CheckResponseAsync(response, apiKey, timeout.Token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        return json.RootElement.GetProperty("data").EnumerateArray().Select(m => m.GetProperty("id").GetString()!)
            .Where(m => (m.StartsWith("gpt-", StringComparison.Ordinal) || m.StartsWith("chatgpt-", StringComparison.Ordinal) ||
                m.StartsWith("o1", StringComparison.Ordinal) || m.StartsWith("o3", StringComparison.Ordinal) || m.StartsWith("o4", StringComparison.Ordinal)) &&
                !new[] { "audio", "realtime", "transcribe", "image", "tts" }.Any(m.Contains))
            .Distinct().Order(StringComparer.Ordinal).ToArray();
    }

    public async IAsyncEnumerable<string> ChatAsync(Settings settings, string apiKey, IReadOnlyList<ChatMessage> conversation,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var input = ConversationContext.Bound(conversation);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var request = Request(HttpMethod.Post, "responses", apiKey);
        request.Content = JsonContent.Create(new
        {
            model = settings.OpenAiModel, instructions = ConversationContext.Instructions(settings),
            input, stream = true, store = false, max_output_tokens = 2048
        });
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        await CheckResponseAsync(response, apiKey, timeout.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        var data = new StringBuilder();
        while (true)
        {
            var line = await reader.ReadLineAsync(timeout.Token);
            if (line is { Length: > 0 })
            {
                if (line.StartsWith("data:", StringComparison.Ordinal)) { if (data.Length > 0) data.Append('\n'); data.Append(line[5..].TrimStart(' ')); }
                continue;
            }
            if (data.Length > 0)
            {
                var payload = data.ToString(); data.Clear();
                if (payload != "[DONE]")
                {
                    using var json = JsonDocument.Parse(payload);
                    var root = json.RootElement;
                    var type = root.GetProperty("type").GetString();
                    if (type is "response.output_text.delta" or "response.refusal.delta")
                    { if (root.TryGetProperty("delta", out var delta) && delta.GetString() is { Length: > 0 } text) yield return text; }
                    else if (type == "response.completed") yield break;
                    else if (type is "response.failed" or "response.incomplete" or "error")
                        throw new InvalidOperationException(ErrorMessage(root, apiKey, type == "response.incomplete"
                            ? "OpenAI could not finish this reply within its output limit. Try a shorter request or another model." : "OpenAI could not complete this request."));
                }
            }
            if (line == null) break;
        }
        throw new IOException("OpenAI closed the response before finishing. Please try again.");
    }

    private static string ErrorMessage(JsonElement root, string key, string fallback)
    {
        if (root.TryGetProperty("response", out var response)) root = response;
        if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object) root = error;
        var detail = root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String ? message.GetString() ?? fallback : fallback;
        detail = detail.Replace(key, "[redacted]", StringComparison.Ordinal);
        return detail[..Math.Min(detail.Length, 400)];
    }
    private static async Task CheckResponseAsync(HttpResponseMessage response, string key, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = "Check your API key, model access, and API billing in OpenAI settings.";
        try { using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token)); detail = ErrorMessage(json.RootElement, key, detail); }
        catch (JsonException) { }
        throw new HttpRequestException($"OpenAI returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
    }
}
