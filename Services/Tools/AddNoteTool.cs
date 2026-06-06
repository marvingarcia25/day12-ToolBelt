using System.Text.Json;

namespace ToolBelt.Services.Tools;

public class AddNoteTool : ITool
{
    private readonly NotesStore _store;

    public AddNoteTool(NotesStore store) => _store = store;

    public string Name => "add_note";
    public string Description => "Saves a note to memory for later retrieval.";
    public object Parameters => new
    {
        type = "object",
        properties = new
        {
            text = new { type = "string", description = "The note text to save." }
        },
        required = new[] { "text" }
    };

    public Task<string> ExecuteAsync(JsonElement args)
    {
        if (!args.TryGetProperty("text", out var textEl))
            return Task.FromResult("Error: missing required argument 'text'.");

        var text = textEl.GetString() ?? string.Empty;
        _store.Add(text);
        return Task.FromResult($"Note saved: \"{text}\"");
    }
}
