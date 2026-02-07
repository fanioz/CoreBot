using System.Text.Json;
using CoreBot.Core.Messages;

namespace CoreBot.Core.Subagents;

/// <summary>
/// State of a subagent
/// </summary>
public enum SubagentState
{
    /// <summary>
    /// Subagent is created but not yet started
    /// </summary>
    Created,

    /// <summary>
    /// Subagent is currently running
    /// </summary>
    Running,

    /// <summary>
    /// Subagent completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Subagent failed with an error
    /// </summary>
    Failed,

    /// <summary>
    /// Subagent was cancelled
    /// </summary>
    Cancelled
}

/// <summary>
/// A subagent for handling long-running background tasks
/// </summary>
public class Subagent
{
    /// <summary>
    /// Unique identifier for the subagent
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name/description of the subagent
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current state of the subagent
    /// </summary>
    public SubagentState State { get; set; } = SubagentState.Created;

    /// <summary>
    /// Platform that initiated the subagent
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// User ID that initiated the subagent
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Original message that triggered the subagent
    /// </summary>
    public UserMessage? TriggerMessage { get; set; }

    /// <summary>
    /// Task/tool being executed by the subagent
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// Parameters for the task
    /// </summary>
    public JsonElement? TaskParameters { get; set; }

    /// <summary>
    /// Progress of the subagent (0-100)
    /// </summary>
    public int Progress { get; set; } = 0;

    /// <summary>
    /// Current status message
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Result of the subagent execution
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Error message if the subagent failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// When the subagent was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the subagent was started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the subagent completed (success or failure)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Custom metadata for the subagent
    /// </summary>
    public Dictionary<string, JsonElement> Metadata { get; set; } = new();

    /// <summary>
    /// Serialize the subagent to JSON
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Deserialize a subagent from JSON
    /// </summary>
    public static Subagent? FromJson(string json)
    {
        return JsonSerializer.Deserialize<Subagent>(json);
    }
}

/// <summary>
/// Message published when a subagent completes
/// </summary>
public class SubagentCompletedMessage : IMessage
{
    /// <summary>
    /// Message ID
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The subagent that completed
    /// </summary>
    public required Subagent Subagent { get; set; }

    /// <summary>
    /// Platform to send the notification to
    /// </summary>
    public required string Platform { get; set; }

    /// <summary>
    /// User to send the notification to
    /// </summary>
    public required string UserId { get; set; }
}
