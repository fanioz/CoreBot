using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreBot.Core.Messages;
using CoreBot.Core.Messaging;

namespace CoreBot.Core.ChatPlatforms;

/// <summary>
/// Chat platform adapter for Feishu
/// </summary>
public class FeishuAdapter : IChatPlatform, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly FeishuConfiguration _config;
    private readonly IMessageBus _messageBus;
    private string? _tenantAccessToken;
    private DateTime _tokenExpiry;
    private bool _isReceiving = false;

    public string PlatformName => "feishu";

    public FeishuAdapter(
        HttpClient httpClient,
        FeishuConfiguration config,
        IMessageBus messageBus)
    {
        _httpClient = httpClient;
        _config = config;
        _messageBus = messageBus;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "CoreBot/1.0");
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // Get tenant access token to verify credentials
        await EnsureTenantAccessTokenAsync(ct);
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _isReceiving = false;
        return Task.CompletedTask;
    }

    public async Task SendMessageAsync(AgentResponse message, CancellationToken ct = default)
    {
        var (openChatId, userId) = ExtractChatInfo(message);
        if (string.IsNullOrEmpty(openChatId) && string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("Cannot extract chat ID or user ID from message", nameof(message));
        }

        await EnsureTenantAccessTokenAsync(ct);

        var requestBody = new
        {
            msg_type = "text",
            receive_id_type = !string.IsNullOrEmpty(openChatId) ? "chat_id" : "open_id",
            receive_id = !string.IsNullOrEmpty(openChatId) ? openChatId : userId,
            content = new
            {
                text = message.Content
            }
        };

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"{_config.ApiBaseUrl}/im/v1/messages?receive_id_type={(!string.IsNullOrEmpty(openChatId) ? "chat_id" : "open_id")}"),
            Headers =
            {
                { "Authorization", $"Bearer {_tenantAccessToken}" }
            },
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
            throw new Exception($"Feishu send message failed: {response.StatusCode} - {responseBody}");
        }
    }

    public Task StartReceivingAsync(CancellationToken ct = default)
    {
        // Feishu uses event subscription (webhooks) for receiving messages, not polling
        // The webhook endpoint should be hosted separately (e.g., in ASP.NET Core)
        // This method is a no-op for webhook-based platforms
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handle incoming event from Feishu
    /// </summary>
    public async Task HandleEventAsync(string eventType, JsonObject eventPayload, CancellationToken ct = default)
    {
        if (eventType != "im.message.receive_v1")
        {
            return;
        }

        var eventObject = eventPayload["event"];
        if (eventObject == null) return;

        await ProcessMessageAsync(eventObject, ct);
    }

    /// <summary>
    /// Verify event signature from Feishu
    /// </summary>
    public bool VerifyEventSignature(string timestamp, string nonce, string body, string signature)
    {
        if (string.IsNullOrEmpty(_config.EncryptKey))
        {
            return false;
        }

        // Compute expected signature
        var computedSignature = ComputeHmacSha256(timestamp + nonce + body, _config.EncryptKey);
        return signature == computedSignature;
    }

    private async Task EnsureTenantAccessTokenAsync(CancellationToken ct)
    {
        // Check if token is still valid (with 5 minute buffer)
        if (_tenantAccessToken != null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
        {
            return;
        }

        // Get new tenant access token
        var requestBody = new
        {
            app_id = _config.AppId,
            app_secret = _config.AppSecret
        };

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"{_config.ApiBaseUrl}/auth/v3/tenant_access_token/internal"),
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
            throw new Exception($"Feishu get tenant token failed: {response.StatusCode} - {responseBody}");
        }

        var json = JsonNode.Parse(responseBody);
        var code = json?["code"]?.GetValue<int>();
        if (code != 0)
        {
            throw new Exception($"Feishu authentication failed: {responseBody}");
        }

        _tenantAccessToken = json?["tenant_access_token"]?.ToString();
        var expireSeconds = json?["expire"]?.GetValue<int>() ?? 7200;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expireSeconds);
    }

    private async Task ProcessMessageAsync(JsonNode eventObject, CancellationToken ct)
    {
        var sender = eventObject?["sender"]?["sender_id"]?["open_id"]?.ToString();
        var messageId = eventObject?["message_id"]?.ToString();
        var createTime = eventObject?["create_time"]?.ToString();
        var messageContent = eventObject?["message"];

        if (string.IsNullOrEmpty(sender) || messageContent == null)
        {
            return;
        }

        var text = messageContent?["content"]?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var userMessage = new UserMessage(
            MessageId: messageId ?? Guid.NewGuid().ToString(),
            Timestamp: long.TryParse(createTime, out var ts)
                ? DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime
                : DateTime.UtcNow,
            Platform: "feishu",
            UserId: sender,
            Content: text
        );

        await _messageBus.PublishAsync(userMessage, ct);
    }

    private static (string? openChatId, string? userId) ExtractChatInfo(AgentResponse message)
    {
        // For messages sent from the bot to the user, we need to find the original chat info
        // This would be stored in the message metadata or conversation context
        // For now, return null - the implementation should extract this from conversation history
        return (null, null);
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToBase64String(hash);
    }

    public async ValueTask DisposeAsync()
    {
        _isReceiving = false;
        await Task.CompletedTask;
    }
}
