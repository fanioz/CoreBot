namespace CoreBot.Core.Messaging;

using CoreBot.Core.Messages;

/// <summary>
/// Message bus for event-driven communication between components
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publish a message to all subscribers
    /// </summary>
    ValueTask PublishAsync<T>(T message, CancellationToken ct = default) where T : IMessage;

    /// <summary>
    /// Subscribe to messages of a specific type
    /// </summary>
    IAsyncEnumerable<T> SubscribeAsync<T>(CancellationToken ct = default) where T : IMessage;
}
