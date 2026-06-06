using System.Text.Json.Nodes;

namespace ToolBelt.Services;

public interface IOllamaClient
{
    IAsyncEnumerable<OllamaChunk> StreamChatAsync(
        IReadOnlyList<JsonNode> messages,
        IReadOnlyList<object> tools,
        CancellationToken ct = default);
}
