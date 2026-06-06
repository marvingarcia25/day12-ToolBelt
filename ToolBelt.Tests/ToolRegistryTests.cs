using System.Text.Json;
using System.Text.Json.Nodes;
using ToolBelt.Services;
using ToolBelt.Services.Tools;

namespace ToolBelt.Tests;

public class ToolRegistryTests
{
    private static ToolRegistry BuildRegistry()
    {
        var store = new NotesStore();
        var tools = new List<ITool>
        {
            new TimeTool(),
            new CalculatorTool(),
            new AddNoteTool(store),
            new SearchNotesTool(store),
            new ListNotesTool(store)
        };
        return new ToolRegistry(tools);
    }

    [Fact]
    public void Schema_IncludesAllFiveTools()
    {
        var registry = BuildRegistry();
        var schema = registry.GetToolsSchema();
        Assert.Equal(5, schema.Count);
    }

    [Fact]
    public void Schema_ContainsExpectedToolNames()
    {
        var registry = BuildRegistry();
        var schema = registry.GetToolsSchema();

        var names = schema
            .Select(s => JsonDocument.Parse(JsonSerializer.Serialize(s)).RootElement)
            .Select(el => el.GetProperty("function").GetProperty("name").GetString())
            .ToHashSet();

        Assert.Contains("get_current_time", names);
        Assert.Contains("calculate", names);
        Assert.Contains("add_note", names);
        Assert.Contains("search_notes", names);
        Assert.Contains("list_notes", names);
    }

    [Fact]
    public async Task Dispatch_KnownTool_ReturnsResult()
    {
        var registry = BuildRegistry();
        var args = JsonDocument.Parse("{\"expression\":\"6*7\"}").RootElement;

        var result = await registry.DispatchAsync("calculate", args);

        Assert.Equal("42", result);
    }

    [Fact]
    public async Task Dispatch_UnknownToolName_ReturnsErrorNotThrows()
    {
        var registry = BuildRegistry();
        var args = JsonDocument.Parse("{}").RootElement;

        var result = await registry.DispatchAsync("nonexistent_tool", args);

        Assert.StartsWith("Error:", result);
    }
}
