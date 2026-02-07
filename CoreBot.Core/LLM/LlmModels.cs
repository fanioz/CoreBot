using System.Text.Json;
using CoreBot.Core.Tools;
using CoreBot.Core.Messages;

namespace CoreBot.Core.LLM;

/// <summary>
/// Request for LLM completion
/// </summary>
public record LlmRequest
{
    /// <summary>
    /// Conversation history
    /// </summary>
    public List<LlmMessage> Messages { get; init; } = new();

    /// <summary>
    /// Tools available for the LLM to call
    /// </summary>
    public List<ToolDefinition>? Tools { get; init; }

    /// <summary>
    /// Maximum tokens in response
    /// </summary>
    public int MaxTokens { get; init; } = 4096;

    /// <summary>
    /// Temperature for generation (0.0 to 1.0)
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Stop sequences
    /// </summary>
    public List<string>? Stop { get; init; }
}

/// <summary>
/// Message in the conversation
/// </summary>
public record LlmMessage
{
    /// <summary>
    /// Role (user, assistant, system, tool)
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Tool calls made by the assistant (optional)
    /// </summary>
    public List<ToolCall>? ToolCalls { get; init; }

    /// <summary>
    /// Tool result message (optional)
    /// </summary>
    public Messages.ToolResult? ToolResult { get; init; }
}

/// <summary>
/// Tool definition for LLM function calling
/// </summary>
public record ToolDefinition
{
    /// <summary>
    /// Tool name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Tool description
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// JSON schema for parameters
    /// </summary>
    public JsonDocument Parameters { get; init; } = JsonDocument.Parse("{}");
}

/// <summary>
/// Response from LLM
/// </summary>
public record LlmResponse
{
    /// <summary>
    /// Generated content
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Tool calls requested by the LLM
    /// </summary>
    public List<ToolCall>? ToolCalls { get; init; }

    /// <summary>
    /// Whether the response was streamed
    /// </summary>
    public bool WasStreamed { get; init; } = false;

    /// <summary>
    /// Error message if request failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Usage information (tokens, etc.)
    /// </summary>
    public LlmUsage? Usage { get; init; }
}

/// <summary>
/// Usage statistics from LLM provider
/// </summary>
public record LlmUsage
{
    /// <summary>
    /// Prompt tokens used
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// Completion tokens generated
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// Total tokens
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}

/// <summary>
/// Stream chunk for streaming responses
/// </summary>
public record LlmStreamChunk
{
    /// <summary>
    /// Content chunk
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Tool calls in this chunk (if present)
    /// </summary>
    public List<ToolCall>? ToolCalls { get; init; }

    /// <summary>
    /// Whether this is the final chunk
    /// </summary>
    public bool IsFinal { get; init; }
}
