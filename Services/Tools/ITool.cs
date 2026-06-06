using System.Text.Json;

namespace ToolBelt.Services.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    // JSON Schema "parameters" object — type/properties/required
    object Parameters { get; }
    Task<string> ExecuteAsync(JsonElement args);
}
