using CoreBot.Core.Messaging;
using CoreBot.Core.Tools;

namespace CoreBot.Core.Skills;

/// <summary>
/// Interface for plugin skills that can extend CoreBot functionality
/// </summary>
public interface ISkill
{
    /// <summary>
    /// Unique name of the skill
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of the skill
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Version of the skill
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Tools provided by this skill
    /// </summary>
    IEnumerable<IToolDefinition> GetTools();

    /// <summary>
    /// Message handlers provided by this skill
    /// </summary>
    IEnumerable<IMessageHandler> GetMessageHandlers();

    /// <summary>
    /// Initialize the skill with dependencies
    /// </summary>
    Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default);

    /// <summary>
    /// Cleanup when the skill is unloaded
    /// </summary>
    Task ShutdownAsync(CancellationToken ct = default);
}

/// <summary>
/// Interface for handling specific message types
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// Message type this handler can process
    /// </summary>
    Type MessageType { get; }

    /// <summary>
    /// Process the message
    /// </summary>
    Task HandleAsync(object message, CancellationToken ct = default);
}
