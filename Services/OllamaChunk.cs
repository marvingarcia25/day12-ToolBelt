using System.Text.Json;

namespace ToolBelt.Services;

public record OllamaToolCall(string Name, JsonElement Args);

// A single streamed chunk from the Ollama NDJSON response.
// Exactly one of Text or ToolCalls will be populated per chunk.
public record OllamaChunk
{
    public string? Text { get; init; }
    public IReadOnlyList<OllamaToolCall>? ToolCalls { get; init; }
}
