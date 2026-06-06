using System.Collections.Concurrent;

namespace ToolBelt.Services;

public class NotesStore
{
    private readonly ConcurrentBag<string> _notes = [];

    public void Add(string text) => _notes.Add(text);

    public IReadOnlyList<string> Search(string query) =>
        _notes
            .Where(n => n.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public IReadOnlyList<string> ListAll() => _notes.ToList();
}
