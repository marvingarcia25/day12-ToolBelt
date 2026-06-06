using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace ToolBelt.Services;

public class ChatStore
{
    private readonly ConcurrentDictionary<string, List<JsonNode>> _sessions = new();

    private const string SystemPrompt =
        "You are ToolBelt, a helpful assistant that uses tools to act, not just talk. " +
        "Tool guidance: use `calculate` ONLY for arithmetic on numbers; never use it to " +
        "echo or restate something the user said. When the user asks you to remember, save, " +
        "note, or store something, call `add_note` with their information. When the user asks " +
        "what they told you, where/what something is, or to recall anything, call " +
        "`search_notes` (or `list_notes` to see everything). Use `get_current_time` for the " +
        "current date or time. If no tool fits, just answer directly.";

    private static List<JsonNode> NewHistory() =>
    [
        new JsonObject { ["role"] = "system", ["content"] = SystemPrompt }
    ];

    public string CreateSession()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        _sessions[id] = NewHistory();
        return id;
    }

    // Returns null if the session does not exist.
    public List<JsonNode>? GetHistory(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var history) ? history : null;

    // Creates the session if it does not already exist, then returns the history list.
    public List<JsonNode> GetOrCreateHistory(string sessionId) =>
        _sessions.GetOrAdd(sessionId, _ => NewHistory());

    public void AppendMessage(string sessionId, JsonNode message)
    {
        var history = GetOrCreateHistory(sessionId);
        lock (history) { history.Add(message); }
    }
}
