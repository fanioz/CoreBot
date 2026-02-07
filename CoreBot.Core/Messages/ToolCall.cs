namespace CoreBot.Core.Messages;

using System.Text.Json;

/// <summary>
/// Represents a tool call requested by the LLM
/// </summary>
public record ToolCall(
    string ToolName,
    JsonElement Parameters
);
