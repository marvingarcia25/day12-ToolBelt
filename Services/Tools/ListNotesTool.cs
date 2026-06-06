using System.Text.Json;

namespace ToolBelt.Services.Tools;

public class ListNotesTool : ITool
{
    private readonly NotesStore _store;

    public ListNotesTool(NotesStore store) => _store = store;

    public string Name => "list_notes";
    public string Description => "Returns all saved notes.";
    public object Parameters => new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>()
    };

    public Task<string> ExecuteAsync(JsonElement args)
    {
        var notes = _store.ListAll();

        if (notes.Count == 0)
            return Task.FromResult("No notes saved yet.");

        return Task.FromResult(string.Join("\n", notes.Select((n, i) => $"{i + 1}. {n}")));
    }
}
