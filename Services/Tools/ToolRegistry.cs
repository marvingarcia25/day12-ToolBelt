using System.Text.Json;

namespace ToolBelt.Services.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools;

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, t => t);
    }

    // Returns the tools array in OpenAI/Ollama function-calling schema format.
    public IReadOnlyList<object> GetToolsSchema() =>
        _tools.Values.Select(t => (object)new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters
            }
        }).ToList();

    public async Task<string> DispatchAsync(string toolName, JsonElement args)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
            return $"Error: unknown tool '{toolName}'.";

        try
        {
            return await tool.ExecuteAsync(args);
        }
        catch (Exception ex)
        {
            return $"Error executing tool '{toolName}': {ex.Message}";
        }
    }
}
