using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ToolBelt.Services.Tools;

public partial class CalculatorTool : ITool
{
    public string Name => "calculate";
    public string Description => "Evaluates a simple arithmetic expression and returns the numeric result.";
    public object Parameters => new
    {
        type = "object",
        properties = new
        {
            expression = new
            {
                type = "string",
                description = "A simple arithmetic expression, e.g. \"2 + 2 * 3\"."
            }
        },
        required = new[] { "expression" }
    };

    [GeneratedRegex(@"^[\d\s+\-*/().]+$")]
    private static partial Regex SafeExpressionRegex();

    public Task<string> ExecuteAsync(JsonElement args)
    {
        if (!args.TryGetProperty("expression", out var exprEl))
            return Task.FromResult("Error: missing required argument 'expression'.");

        var expression = exprEl.GetString() ?? string.Empty;

        if (!SafeExpressionRegex().IsMatch(expression))
            return Task.FromResult("Error: expression contains disallowed characters. Only digits, spaces, and + - * / ( ) . are permitted.");

        try
        {
            var result = new DataTable().Compute(expression, null);
            return Task.FromResult(Convert.ToString(result) ?? "null");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error evaluating expression: {ex.Message}");
        }
    }
}
