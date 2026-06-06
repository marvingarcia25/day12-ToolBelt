using System.Text.Json;
using ToolBelt.Services.Tools;

namespace ToolBelt.Tests;

public class CalculatorToolTests
{
    private static JsonElement ArgsFor(string expression) =>
        JsonDocument.Parse($"{{\"expression\":\"{expression}\"}}").RootElement;

    private readonly CalculatorTool _tool = new();

    [Theory]
    [InlineData("2+2", "4")]
    [InlineData("3 * 4", "12")]
    [InlineData("10 / 2", "5")]
    [InlineData("(2 + 3) * 4", "20")]
    [InlineData("23 * 19", "437")]
    public async Task ValidExpression_ReturnsCorrectResult(string expression, string expected)
    {
        var result = await _tool.ExecuteAsync(ArgsFor(expression));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2; DROP TABLE notes")]
    [InlineData("__import__('os')")]
    [InlineData("exec('print(1)')")]
    [InlineData("System.IO.File.Delete('x')")]
    public async Task UnsafeExpression_ReturnsError(string expression)
    {
        var result = await _tool.ExecuteAsync(ArgsFor(expression));
        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public async Task MissingExpressionArgument_ReturnsError()
    {
        var args = JsonDocument.Parse("{}").RootElement;
        var result = await _tool.ExecuteAsync(args);
        Assert.StartsWith("Error:", result);
    }
}
