using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ToolBelt.Services;
using ToolBelt.Services.Tools;

namespace ToolBelt.Tests;

public class AssistantServiceTests
{
    // Fake client that returns a configured sequence of chunk lists, one list per turn.
    private sealed class FakeOllamaClient : IOllamaClient
    {
        private readonly Queue<IReadOnlyList<OllamaChunk>> _turns;

        public FakeOllamaClient(params IReadOnlyList<OllamaChunk>[] turns)
        {
            _turns = new Queue<IReadOnlyList<OllamaChunk>>(turns);
        }

        public async IAsyncEnumerable<OllamaChunk> StreamChatAsync(
            IReadOnlyList<JsonNode> messages,
            IReadOnlyList<object> tools,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var chunks = _turns.Dequeue();
            foreach (var chunk in chunks)
            {
                yield return chunk;
                await Task.Yield();
            }
        }
    }

    private static (AssistantService service, ChatStore store) BuildService(IOllamaClient fakeClient)
    {
        var notesStore = new NotesStore();
        var tools = new List<ITool>
        {
            new TimeTool(),
            new CalculatorTool(),
            new AddNoteTool(notesStore),
            new SearchNotesTool(notesStore),
            new ListNotesTool(notesStore)
        };
        var registry = new ToolRegistry(tools);
        var chatStore = new ChatStore();
        var service = new AssistantService(fakeClient, registry, chatStore);
        return (service, chatStore);
    }

    [Fact]
    public async Task AgentLoop_ToolCallThenFinalAnswer_EmitsExpectedEvents()
    {
        // Turn 1: model asks to calculate "6*7"
        var calculatorArgs = JsonDocument.Parse("{\"expression\":\"6*7\"}").RootElement;
        var turn1 = new List<OllamaChunk>
        {
            new OllamaChunk { ToolCalls = [new OllamaToolCall("calculate", calculatorArgs)] }
        };

        // Turn 2: model gives the final text answer
        var turn2 = new List<OllamaChunk>
        {
            new OllamaChunk { Text = "The answer is " },
            new OllamaChunk { Text = "42." }
        };

        var fakeClient = new FakeOllamaClient(turn1, turn2);
        var (service, store) = BuildService(fakeClient);

        var sessionId = store.CreateSession();
        var emittedEvents = new List<SseEvent>();

        await service.RunAsync(sessionId, "what is 6*7?",
            (evt, _) => { emittedEvents.Add(evt); return Task.CompletedTask; });

        // Tool was called — tool_call and tool_result events emitted
        Assert.Contains(emittedEvents, e => e.Type == "tool_call");
        Assert.Contains(emittedEvents, e => e.Type == "tool_result");

        // Final tokens were streamed
        Assert.Contains(emittedEvents, e => e.Type == "token");

        // Loop terminated with a done event
        Assert.Contains(emittedEvents, e => e.Type == "done");

        // No error events
        Assert.DoesNotContain(emittedEvents, e => e.Type == "error");

        // History: user + assistant(tool_calls) + tool_result + assistant(final)
        var history = store.GetOrCreateHistory(sessionId);
        Assert.True(history.Count >= 4, $"Expected at least 4 history entries, got {history.Count}");

        var roles = history.Select(n => n["role"]?.GetValue<string>()).ToList();
        Assert.Contains("user", roles);
        Assert.Contains("assistant", roles);
        Assert.Contains("tool", roles);
    }

    [Fact]
    public async Task AgentLoop_DirectTextAnswer_EmitsDoneWithNoToolEvents()
    {
        var turn1 = new List<OllamaChunk>
        {
            new OllamaChunk { Text = "Hello there!" }
        };

        var fakeClient = new FakeOllamaClient(turn1);
        var (service, store) = BuildService(fakeClient);
        var sessionId = store.CreateSession();
        var emittedEvents = new List<SseEvent>();

        await service.RunAsync(sessionId, "hi",
            (evt, _) => { emittedEvents.Add(evt); return Task.CompletedTask; });

        Assert.Contains(emittedEvents, e => e.Type == "token");
        Assert.Contains(emittedEvents, e => e.Type == "done");
        Assert.DoesNotContain(emittedEvents, e => e.Type == "tool_call");
        Assert.DoesNotContain(emittedEvents, e => e.Type == "tool_result");
    }

    [Fact]
    public async Task AgentLoop_ToolCallExecutes_ResultAppearsInHistory()
    {
        // Model calls add_note
        var addArgs = JsonDocument.Parse("{\"text\":\"buy milk\"}").RootElement;
        var turn1 = new List<OllamaChunk>
        {
            new OllamaChunk { ToolCalls = [new OllamaToolCall("add_note", addArgs)] }
        };
        var turn2 = new List<OllamaChunk>
        {
            new OllamaChunk { Text = "Done, I've noted it." }
        };

        var fakeClient = new FakeOllamaClient(turn1, turn2);
        var (service, store) = BuildService(fakeClient);
        var sessionId = store.CreateSession();
        var toolResults = new List<string>();

        await service.RunAsync(sessionId, "remember: buy milk",
            (evt, _) =>
            {
                if (evt.Type == "tool_result")
                {
                    var json = JsonSerializer.Serialize(evt.Payload);
                    using var doc = JsonDocument.Parse(json);
                    toolResults.Add(doc.RootElement.GetProperty("result").GetString() ?? "");
                }
                return Task.CompletedTask;
            });

        // The tool_result content should confirm the note was saved
        Assert.Single(toolResults);
        Assert.Contains("buy milk", toolResults[0]);

        // History should include a tool message with the result
        var history = store.GetOrCreateHistory(sessionId);
        var toolMessage = history.FirstOrDefault(n => n["role"]?.GetValue<string>() == "tool");
        Assert.NotNull(toolMessage);
        Assert.Contains("buy milk", toolMessage["content"]?.GetValue<string>() ?? "");
    }
}
