using System.Text.Json;

namespace ToolBelt.Services.Tools;

public class TimeTool : ITool
{
    public string Name => "get_current_time";
    public string Description => "Returns the current server local date and time as a string.";
    public object Parameters => new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>()
    };

    public Task<string> ExecuteAsync(JsonElement args) =>
        Task.FromResult(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
}
