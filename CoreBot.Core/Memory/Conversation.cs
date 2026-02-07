using System.Text.Json;

namespace CoreBot.Core.Memory;

/// <summary>
/// Represents a single message in a conversation
/// </summary>
public record StoredMessage
{
    /// <summary>
    /// Role of the message sender (user, assistant, tool)
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Tool calls made by the assistant (optional)
    /// </summary>
    public List<ToolCallInfo>? ToolCalls { get; init; }

    /// <summary>
    /// Tool name for tool result messages (optional)
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// Result from tool execution (optional)
    /// </summary>
    public string? Result { get; init; }
}

/// <summary>
/// Information about a tool call
/// </summary>
public record ToolCallInfo
{
    /// <summary>
    /// Name of the tool to call
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Parameters for the tool call as JSON
    /// </summary>
    public JsonElement Parameters { get; init; }
}

/// <summary>
/// Represents a conversation with stored messages
/// </summary>
public class Conversation
{
    /// <summary>
    /// Unique identifier for this conversation
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// Chat platform (telegram, whatsapp, feishu)
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// User ID on the platform
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Messages in this conversation
    /// </summary>
    public List<StoredMessage> Messages { get; set; } = new();

    /// <summary>
    /// File path where this conversation is stored
    /// </summary>
    public string? FilePath { get; set; }
}
