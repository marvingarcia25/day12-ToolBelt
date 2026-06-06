using System.Text.Json;

namespace ToolBelt.Services.Tools;

public class SearchNotesTool : ITool
{
    private readonly NotesStore _store;

    public SearchNotesTool(NotesStore store) => _store = store;

    public string Name => "search_notes";
    public string Description => "Returns saved notes whose text contains the given query (case-insensitive).";
    public object Parameters => new
    {
        type = "object",
        properties = new
        {
            query = new { type = "string", description = "The text to search for within saved notes." }
        },
        required = new[] { "query" }
    };

    public Task<string> ExecuteAsync(JsonElement args)
    {
        if (!args.TryGetProperty("query", out var queryEl))
            return Task.FromResult("Error: missing required argument 'query'.");

        var query = queryEl.GetString() ?? string.Empty;
        var results = _store.Search(query);

        if (results.Count == 0)
            return Task.FromResult($"No notes found matching \"{query}\".");

        return Task.FromResult(string.Join("\n", results.Select((n, i) => $"{i + 1}. {n}")));
    }
}
