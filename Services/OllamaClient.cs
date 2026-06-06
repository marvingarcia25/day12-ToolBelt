using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ToolBelt.Services;

public class OllamaClient : IOllamaClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaClient(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _model = config["Ollama:Model"] ?? "llama3.2:3b";
    }

    public async IAsyncEnumerable<OllamaChunk> StreamChatAsync(
        IReadOnlyList<JsonNode> messages,
        IReadOnlyList<object> tools,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            model = _model,
            messages,
            tools,
            stream = true
        });

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/chat")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var http = _httpClientFactory.CreateClient();
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaChunk? chunk;
            try { chunk = ParseChunk(line); }
            catch { continue; }

            if (chunk is not null) yield return chunk;
        }
    }

    private static OllamaChunk? ParseChunk(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (!root.TryGetProperty("message", out var message)) return null;

        // Tool calls arrive in a chunk (usually with empty content).
        if (message.TryGetProperty("tool_calls", out var toolCallsEl) &&
            toolCallsEl.ValueKind == JsonValueKind.Array &&
            toolCallsEl.GetArrayLength() > 0)
        {
            var calls = new List<OllamaToolCall>();
            foreach (var callEl in toolCallsEl.EnumerateArray())
            {
                if (!callEl.TryGetProperty("function", out var fn)) continue;
                var name = fn.GetProperty("name").GetString() ?? string.Empty;
                var args = fn.TryGetProperty("arguments", out var argsEl)
                    ? argsEl.Clone()
                    : JsonDocument.Parse("{}").RootElement;
                calls.Add(new OllamaToolCall(name, args));
            }
            if (calls.Count > 0)
                return new OllamaChunk { ToolCalls = calls };
        }

        // Content text token.
        if (message.TryGetProperty("content", out var contentEl))
        {
            var text = contentEl.GetString();
            if (!string.IsNullOrEmpty(text))
                return new OllamaChunk { Text = text };
        }

        return null;
    }
}
