namespace CoreBot.Core.Messages;

/// <summary>
/// Base interface for all messages in the system
/// </summary>
public interface IMessage
{
    /// <summary>
    /// Unique identifier for this message
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    DateTime Timestamp { get; }
}
