using System.Text.Json;
using CoreBot.Core.Configuration;

namespace CoreBot.Core.Memory;

/// <summary>
/// File-based JSON implementation of memory store
/// </summary>
public class FileMemoryStore : IMemoryStore
{
    private readonly string _memoryDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileMemoryStore()
    {
        // Use project-local .corebot/memory for development, or user home for production
        var projectDir = Directory.GetCurrentDirectory();
        var projectMemoryDir = Path.Combine(projectDir, ".corebot", "memory");

        if (Directory.Exists(projectMemoryDir))
        {
            _memoryDirectory = projectMemoryDir;
        }
        else
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _memoryDirectory = Path.Combine(homeDir, ".corebot", "memory");
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Get or create conversation ID for a user
    /// </summary>
    public async Task<string> GetOrCreateConversationIdAsync(string platform, string userId, CancellationToken ct = default)
    {
        var userMemoryDir = Path.Combine(_memoryDirectory, platform, userId);

        if (!Directory.Exists(userMemoryDir))
        {
            Directory.CreateDirectory(userMemoryDir);
        }

        // Get the most recent conversation file or create a new ID
        var conversationFiles = Directory.GetFiles(userMemoryDir, "*.json")
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToList();

        if (conversationFiles.Count > 0)
        {
            var existingConversation = await LoadConversationAsync(Path.GetFileNameWithoutExtension(conversationFiles[0]), ct);
            if (existingConversation != null)
            {
                return existingConversation.ConversationId;
            }
        }

        // Create new conversation ID: {platform}_{userId}_{date}
        var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
        var conversationId = $"{platform}_{userId}_{dateStr}";

        // Create empty conversation file
        var newConversation = new Conversation
        {
            ConversationId = conversationId,
            Platform = platform,
            UserId = userId,
            FilePath = Path.Combine(userMemoryDir, $"{conversationId}.json")
        };

        await SaveConversationAsync(newConversation, ct);
        return conversationId;
    }

    /// <summary>
    /// Save a message to the conversation history
    /// </summary>
    public async Task SaveMessageAsync(string platform, string userId, StoredMessage message, CancellationToken ct = default)
    {
        var conversationId = await GetOrCreateConversationIdAsync(platform, userId, ct);
        var conversation = await LoadConversationAsync(conversationId, ct) ?? new Conversation
        {
            ConversationId = conversationId,
            Platform = platform,
            UserId = userId
        };

        conversation.Messages.Add(message);
        await SaveConversationAsync(conversation, ct);
    }

    /// <summary>
    /// Get conversation history for a user
    /// </summary>
    public async Task<List<StoredMessage>> GetHistoryAsync(string platform, string userId, int limit = 100, CancellationToken ct = default)
    {
        var conversationId = await GetOrCreateConversationIdAsync(platform, userId, ct);
        var conversation = await LoadConversationAsync(conversationId, ct);

        if (conversation == null)
        {
            return new List<StoredMessage>();
        }

        // Return the last N messages
        var skip = Math.Max(0, conversation.Messages.Count - limit);
        return conversation.Messages.Skip(skip).ToList();
    }

    /// <summary>
    /// Load a conversation by ID
    /// </summary>
    public async Task<Conversation?> LoadConversationAsync(string conversationId, CancellationToken ct = default)
    {
        // Search for the conversation file
        var conversationFiles = Directory.GetFiles(_memoryDirectory, "*.json", SearchOption.AllDirectories)
            .Where(f => Path.GetFileNameWithoutExtension(f) == conversationId)
            .ToList();

        if (conversationFiles.Count == 0)
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(conversationFiles[0], ct);
        var conversation = JsonSerializer.Deserialize<Conversation>(json, _jsonOptions);

        if (conversation != null)
        {
            conversation.FilePath = conversationFiles[0];
        }

        return conversation;
    }

    /// <summary>
    /// Save a conversation to disk
    /// </summary>
    private async Task SaveConversationAsync(Conversation conversation, CancellationToken ct = default)
    {
        var userMemoryDir = Path.Combine(_memoryDirectory, conversation.Platform, conversation.UserId);

        if (!Directory.Exists(userMemoryDir))
        {
            Directory.CreateDirectory(userMemoryDir);
        }

        var filePath = Path.Combine(userMemoryDir, $"{conversation.ConversationId}.json");
        conversation.FilePath = filePath;

        var json = JsonSerializer.Serialize(conversation, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct);
    }
}
