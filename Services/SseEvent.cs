namespace ToolBelt.Services;

public record SseEvent(string Type, object? Payload = null);
