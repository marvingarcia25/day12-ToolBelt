using System.Text.Json;
using ToolBelt.Services;
using ToolBelt.Services.Tools;

namespace ToolBelt.Tests;

public class NotesTests
{
    private static JsonElement ArgsFor(string key, string value) =>
        JsonDocument.Parse($"{{\"{key}\":\"{value}\"}}").RootElement;

    private static JsonElement EmptyArgs() =>
        JsonDocument.Parse("{}").RootElement;

    [Fact]
    public async Task AddNote_ConfirmsAndPersists()
    {
        var store = new NotesStore();
        var tool = new AddNoteTool(store);

        var result = await tool.ExecuteAsync(ArgsFor("text", "parked on level 3"));

        Assert.Contains("parked on level 3", result);
        Assert.Single(store.ListAll());
    }

    [Fact]
    public async Task SearchNotes_ReturnsMatch()
    {
        var store = new NotesStore();
        store.Add("parked on level 3");
        store.Add("meeting at 9am");

        var searchTool = new SearchNotesTool(store);
        var result = await searchTool.ExecuteAsync(ArgsFor("query", "level"));

        Assert.Contains("level 3", result);
        Assert.DoesNotContain("9am", result);
    }

    [Fact]
    public async Task SearchNotes_ReturnsNoMatchMessage()
    {
        var store = new NotesStore();
        store.Add("parked on level 3");

        var searchTool = new SearchNotesTool(store);
        var result = await searchTool.ExecuteAsync(ArgsFor("query", "dentist"));

        Assert.Contains("No notes found", result);
    }

    [Fact]
    public async Task ListNotes_ReturnsAllNotes()
    {
        var store = new NotesStore();
        store.Add("note one");
        store.Add("note two");

        var listTool = new ListNotesTool(store);
        var result = await listTool.ExecuteAsync(EmptyArgs());

        Assert.Contains("note one", result);
        Assert.Contains("note two", result);
    }

    [Fact]
    public async Task ListNotes_EmptyStore_ReturnsEmptyMessage()
    {
        var store = new NotesStore();
        var listTool = new ListNotesTool(store);
        var result = await listTool.ExecuteAsync(EmptyArgs());
        Assert.Contains("No notes", result);
    }
}
