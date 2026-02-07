using CoreBot.Core.Messages;

namespace CoreBot.Core.ChatPlatforms;

/// <summary>
/// Interface for chat platform adapters
/// </summary>
public interface IChatPlatform
{
    /// <summary>
    /// Platform name (e.g., "telegram", "whatsapp", "feishu")
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Connect to the platform
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Disconnect from the platform
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Send a message to the platform
    /// </summary>
    Task SendMessageAsync(AgentResponse message, CancellationToken ct = default);

    /// <summary>
    /// Start receiving messages from the platform
    /// </summary>
    Task StartReceivingAsync(CancellationToken ct = default);
}
