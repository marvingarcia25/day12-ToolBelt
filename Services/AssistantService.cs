using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ToolBelt.Services.Tools;

namespace ToolBelt.Services;

public class AssistantService
{
    private readonly IOllamaClient _ollama;
    private readonly ToolRegistry _toolRegistry;
    private readonly ChatStore _chatStore;

    private const int MaxIterations = 6;

    public AssistantService(IOllamaClient ollama, ToolRegistry toolRegistry, ChatStore chatStore)
    {
        _ollama = ollama;
        _toolRegistry = toolRegistry;
        _chatStore = chatStore;
    }

    // Runs the agent loop for the given session. Each SSE event is handed to the writer delegate.
    // This signature keeps AssistantService testable: tests pass a list-appending lambda; Program.cs
    // wires it to write "data: {...}\n\n" to HttpContext.Response.
    public async Task RunAsync(
        string sessionId,
        string userMessage,
        Func<SseEvent, CancellationToken, Task> writeEvent,
        CancellationToken ct = default)
    {
        var history = _chatStore.GetOrCreateHistory(sessionId);
        var tools = _toolRegistry.GetToolsSchema();

        lock (history)
        {
            history.Add(JsonNode.Parse(JsonSerializer.Serialize(new { role = "user", content = userMessage }))!);
        }

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            List<JsonNode> snapshot;
            lock (history) { snapshot = [.. history]; }

            var contentBuffer = new StringBuilder();
            List<OllamaToolCall>? pendingToolCalls = null;

            try
            {
                await foreach (var chunk in _ollama.StreamChatAsync(snapshot, tools, ct))
                {
                    if (chunk.ToolCalls is { Count: > 0 })
                    {
                        pendingToolCalls = [.. chunk.ToolCalls];
                    }
                    else if (chunk.Text is not null)
                    {
                        contentBuffer.Append(chunk.Text);
                        await writeEvent(new SseEvent("token", new { text = chunk.Text }), ct);
                    }
                }
            }
            catch (HttpRequestException)
            {
                await writeEvent(new SseEvent("error", new { message = "Can't reach Ollama at localhost:11434 — is it running?" }), ct);
                return;
            }

            if (pendingToolCalls is { Count: > 0 })
            {
                // Append the assistant message containing the tool_calls to history.
                var assistantMessage = BuildAssistantToolCallMessage(contentBuffer.ToString(), pendingToolCalls);
                lock (history) { history.Add(assistantMessage); }

                // Execute each tool call and append tool result messages.
                foreach (var toolCall in pendingToolCalls)
                {
                    await writeEvent(new SseEvent("tool_call", new { name = toolCall.Name, args = toolCall.Args }), ct);

                    var result = await _toolRegistry.DispatchAsync(toolCall.Name, toolCall.Args);

                    await writeEvent(new SseEvent("tool_result", new { name = toolCall.Name, result }), ct);

                    var toolResultMessage = JsonNode.Parse(JsonSerializer.Serialize(new { role = "tool", content = result }))!;
                    lock (history) { history.Add(toolResultMessage); }
                }

                // Loop back — let the model continue with the tool results in context.
                continue;
            }

            // Turn ended with text only — final answer already streamed.
            var finalMessage = JsonNode.Parse(JsonSerializer.Serialize(new
            {
                role = "assistant",
                content = contentBuffer.ToString()
            }))!;
            lock (history) { history.Add(finalMessage); }

            await writeEvent(new SseEvent("done"), ct);
            return;
        }

        // Max iterations reached without a clean text-only turn.
        await writeEvent(new SseEvent("error", new { message = "Max iterations reached without a final answer." }), ct);
        await writeEvent(new SseEvent("done"), ct);
    }

    private static JsonNode BuildAssistantToolCallMessage(string content, List<OllamaToolCall> toolCalls)
    {
        var callNodes = toolCalls.Select(tc => new JsonObject
        {
            ["function"] = new JsonObject
            {
                ["name"] = tc.Name,
                ["arguments"] = JsonNode.Parse(tc.Args.GetRawText())
            }
        }).ToArray();

        var node = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = content,
            ["tool_calls"] = new JsonArray(callNodes.Cast<JsonNode>().ToArray())
        };
        return node;
    }
}
