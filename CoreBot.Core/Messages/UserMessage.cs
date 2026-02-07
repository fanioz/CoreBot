namespace CoreBot.Core.Messages;

/// <summary>
/// Message sent by a user from a chat platform
/// </summary>
public record UserMessage(
    string MessageId,
    DateTime Timestamp,
    string Platform,
    string UserId,
    string Content
) : IMessage;
