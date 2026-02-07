namespace CoreBot.Core.Tools;

using System.Text.Json;

/// <summary>
/// Result from executing a tool
/// </summary>
public record ToolResult
{
    /// <summary>
    /// Whether the tool execution succeeded
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Result message or error
    /// </summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>
    /// Optional error details
    /// </summary>
    public string? Error { get; init; }
}
