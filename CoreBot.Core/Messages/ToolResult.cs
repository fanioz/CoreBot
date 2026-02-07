namespace CoreBot.Core.Messages;

/// <summary>
/// Result from executing a tool
/// </summary>
public record ToolResult(
    string MessageId,
    DateTime Timestamp,
    string ToolName,
    bool Success,
    string Result
) : IMessage;
