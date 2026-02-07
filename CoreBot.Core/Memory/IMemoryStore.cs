namespace CoreBot.Core.Memory;

using CoreBot.Core.Messages;

/// <summary>
/// Interface for persistent storage of conversation history
/// </summary>
public interface IMemoryStore
{
    /// <summary>
    /// Save a message to disk
    /// </summary>
    Task SaveMessageAsync(string platform, string userId, StoredMessage message, CancellationToken ct = default);

    /// <summary>
    /// Get conversation history for a user
    /// </summary>
    Task<List<StoredMessage>> GetHistoryAsync(string platform, string userId, int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Load a conversation by ID
    /// </summary>
    Task<Conversation?> LoadConversationAsync(string conversationId, CancellationToken ct = default);

    /// <summary>
    /// Create or get a conversation ID for a user
    /// </summary>
    Task<string> GetOrCreateConversationIdAsync(string platform, string userId, CancellationToken ct = default);
}
