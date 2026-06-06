using System.Text;
using System.Text.Json;
using ToolBelt.Services;
using ToolBelt.Services.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddSingleton<NotesStore>();

// Register all tools so they can be injected into ToolRegistry.
builder.Services.AddSingleton<ITool, TimeTool>();
builder.Services.AddSingleton<ITool, CalculatorTool>();
builder.Services.AddSingleton<ITool>(sp => new AddNoteTool(sp.GetRequiredService<NotesStore>()));
builder.Services.AddSingleton<ITool>(sp => new SearchNotesTool(sp.GetRequiredService<NotesStore>()));
builder.Services.AddSingleton<ITool>(sp => new ListNotesTool(sp.GetRequiredService<NotesStore>()));

builder.Services.AddSingleton<ToolRegistry>();
builder.Services.AddSingleton<ChatStore>();
builder.Services.AddSingleton<IOllamaClient, OllamaClient>();
builder.Services.AddSingleton<AssistantService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// POST /api/session — create a new chat session, return its id.
app.MapPost("/api/session", (ChatStore store) =>
{
    var sessionId = store.CreateSession();
    return Results.Ok(new { sessionId });
});

// POST /api/chat — run the agent loop for a turn, SSE-streaming events back.
app.MapPost("/api/chat", async (ChatRequest req, HttpContext ctx, AssistantService assistant) =>
{
    var sessionId = req.SessionId ?? ctx.RequestServices.GetRequiredService<ChatStore>().CreateSession();

    ctx.Response.Headers["Content-Type"] = "text/event-stream";
    ctx.Response.Headers["Cache-Control"] = "no-cache";
    ctx.Response.Headers["X-Accel-Buffering"] = "no";

    async Task WriteEventAsync(SseEvent evt, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(BuildEventPayload(evt));
        await ctx.Response.WriteAsync($"data: {json}\n\n", Encoding.UTF8, ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    try
    {
        await assistant.RunAsync(sessionId, req.Message, WriteEventAsync, ctx.RequestAborted);
    }
    catch (OperationCanceledException) { }
});

app.MapFallbackToFile("index.html");
app.Run();

static object BuildEventPayload(SseEvent evt) => evt.Type switch
{
    "token"       => new { type = "token",       text    = GetPayloadText(evt) },
    "tool_call"   => new { type = "tool_call",   name    = GetPayloadName(evt), args   = GetPayloadArgs(evt) },
    "tool_result" => new { type = "tool_result", name    = GetPayloadName(evt), result = GetPayloadResult(evt) },
    "done"        => new { type = "done" },
    "error"       => new { type = "error",       message = GetPayloadMessage(evt) },
    _             => new { type = evt.Type }
};

static string GetPayloadText(SseEvent evt)
{
    if (evt.Payload is { } p)
    {
        var json = JsonSerializer.Serialize(p);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("text", out var el)) return el.GetString() ?? "";
    }
    return "";
}

static string GetPayloadName(SseEvent evt)
{
    if (evt.Payload is { } p)
    {
        var json = JsonSerializer.Serialize(p);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("name", out var el)) return el.GetString() ?? "";
    }
    return "";
}

static object? GetPayloadArgs(SseEvent evt)
{
    if (evt.Payload is { } p)
    {
        var json = JsonSerializer.Serialize(p);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("args", out var el)) return JsonSerializer.Deserialize<object>(el.GetRawText());
    }
    return null;
}

static string GetPayloadResult(SseEvent evt)
{
    if (evt.Payload is { } p)
    {
        var json = JsonSerializer.Serialize(p);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("result", out var el)) return el.GetString() ?? "";
    }
    return "";
}

static string GetPayloadMessage(SseEvent evt)
{
    if (evt.Payload is { } p)
    {
        var json = JsonSerializer.Serialize(p);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("message", out var el)) return el.GetString() ?? "";
    }
    return "";
}

record ChatRequest(string? SessionId, string Message);
