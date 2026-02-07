namespace CoreBot.Core.Tools;

using System.Text.Json;

/// <summary>
/// Definition of a tool that can be executed by the AI agent
/// </summary>
public interface IToolDefinition
{
    /// <summary>
    /// Unique name of the tool
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// JSON schema for the tool's parameters
    /// </summary>
    JsonDocument GetSchema();

    /// <summary>
    /// Execute the tool with given parameters
    /// </summary>
    Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct = default);
}
