using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreBot.Core.Messages;
using CoreBot.Core.Messaging;

namespace CoreBot.Core.ChatPlatforms;

/// <summary>
/// Chat platform adapter for Telegram
/// </summary>
public class TelegramAdapter : IChatPlatform, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TelegramConfiguration _config;
    private readonly IMessageBus _messageBus;
    private int _offset = 0;
    private bool _isReceiving = false;

    public string PlatformName => "telegram";

    public TelegramAdapter(
        HttpClient httpClient,
        TelegramConfiguration config,
        IMessageBus messageBus)
    {
        _httpClient = httpClient;
        _config = config;
        _messageBus = messageBus;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "CoreBot/1.0");
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // Verify bot token by getting bot info
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"https://api.telegram.org/bot{_config.BotToken}/getMe")
        };

        var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Telegram connection failed: {response.StatusCode} - {responseBody}");
        }

        var json = JsonNode.Parse(responseBody);
        if (json?["ok"]?.GetValue<bool>() != true)
        {
            throw new Exception($"Telegram authentication failed: {responseBody}");
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _isReceiving = false;
        return Task.CompletedTask;
    }

    public async Task SendMessageAsync(AgentResponse message, CancellationToken ct = default)
    {
        var chatId = ExtractChatId(message);
        if (string.IsNullOrEmpty(chatId))
        {
            throw new ArgumentException("Cannot extract chat ID from message", nameof(message));
        }

        var requestBody = new
        {
            chat_id = chatId,
            text = message.Content
        };

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"https://api.telegram.org/bot{_config.BotToken}/sendMessage"),
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            )
        };

        var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Telegram send message failed: {response.StatusCode} - {responseBody}");
        }
    }

    public async Task StartReceivingAsync(CancellationToken ct = default)
    {
        _isReceiving = true;

        while (_isReceiving && !ct.IsCancellationRequested)
        {
            try
            {
                await PollForUpdatesAsync(ct);
            }
            catch (Exception ex)
            {
                // Log error and continue polling
                await Task.Delay(_config.RetryDelaySeconds * 1000, ct);
            }
        }
    }

    private async Task PollForUpdatesAsync(CancellationToken ct)
    {
        var requestBody = new
        {
            offset = _offset,
            timeout = _config.PollingTimeout
        };

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"https://api.telegram.org/bot{_config.BotToken}/getUpdates"),
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            )
        };

        var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Telegram getUpdates failed: {response.StatusCode} - {responseBody}");
        }

        var json = JsonNode.Parse(responseBody);
        if (json?["ok"]?.GetValue<bool>() != true)
        {
            throw new Exception($"Telegram getUpdates error: {responseBody}");
        }

        var updates = json?["result"]?.AsArray();
        if (updates == null || updates.Count == 0)
        {
            return;
        }

        foreach (var update in updates)
        {
            await ProcessUpdateAsync(update, ct);
        }

        // Update offset to the last update + 1
        var lastUpdateId = updates.Last()?["update_id"]?.GetValue<int>() ?? 0;
        if (lastUpdateId > 0)
        {
            _offset = lastUpdateId + 1;
        }
    }

    private async Task ProcessUpdateAsync(JsonNode update, CancellationToken ct)
    {
        var message = update?["message"];
        if (message == null) return;

        var chatId = message?["chat"]?["id"]?.ToString();
        var userId = message?["from"]?["id"]?.ToString();
        var text = message?["text"]?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(chatId) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(text))
        {
            return;
        }

        var userMessage = new UserMessage(
            MessageId: Guid.NewGuid().ToString(),
            Timestamp: DateTime.UtcNow,
            Platform: "telegram",
            UserId: userId,
            Content: text
        );

        await _messageBus.PublishAsync(userMessage, ct);
    }

    private static string? ExtractChatId(AgentResponse message)
    {
        // For messages sent from the bot to the user, we need to find the original chat ID
        // This would be stored in the message metadata or conversation context
        // For now, return null - the implementation should extract this from conversation history
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        _isReceiving = false;
        await Task.CompletedTask;
    }
}
