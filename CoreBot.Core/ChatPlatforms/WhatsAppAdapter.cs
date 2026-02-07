using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreBot.Core.Messages;
using CoreBot.Core.Messaging;

namespace CoreBot.Core.ChatPlatforms;

/// <summary>
/// Chat platform adapter for WhatsApp
/// </summary>
public class WhatsAppAdapter : IChatPlatform, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppConfiguration _config;
    private readonly IMessageBus _messageBus;
    private bool _isReceiving = false;

    public string PlatformName => "whatsapp";

    public WhatsAppAdapter(
        HttpClient httpClient,
        WhatsAppConfiguration config,
        IMessageBus messageBus)
    {
        _httpClient = httpClient;
        _config = config;
        _messageBus = messageBus;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "CoreBot/1.0");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.AccessToken}");
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // Verify access token by getting phone number info
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"{_config.ApiBaseUrl}/{_config.PhoneNumberId}")
        };

        var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"WhatsApp connection failed: {response.StatusCode} - {responseBody}");
        }

        var json = JsonNode.Parse(responseBody);
        var error = json?["error"];
        if (error != null)
        {
            throw new Exception($"WhatsApp authentication failed: {error["message"]?.ToString()}");
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _isReceiving = false;
        return Task.CompletedTask;
    }

    public async Task SendMessageAsync(AgentResponse message, CancellationToken ct = default)
    {
        var phoneNumber = ExtractPhoneNumber(message);
        if (string.IsNullOrEmpty(phoneNumber))
        {
            throw new ArgumentException("Cannot extract phone number from message", nameof(message));
        }

        var requestBody = new
        {
            messaging_product = "whatsapp",
            to = phoneNumber,
            type = "text",
            text = new
            {
                body = message.Content
            }
        };

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"{_config.ApiBaseUrl}/{_config.PhoneNumberId}/messages"),
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
            throw new Exception($"WhatsApp send message failed: {response.StatusCode} - {responseBody}");
        }
    }

    public Task StartReceivingAsync(CancellationToken ct = default)
    {
        // WhatsApp uses webhooks for receiving messages, not polling
        // The webhook endpoint should be hosted separately (e.g., in ASP.NET Core)
        // This method is a no-op for webhook-based platforms
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handle incoming webhook message from WhatsApp
    /// </summary>
    public async Task HandleWebhookAsync(JsonObject webhookPayload, CancellationToken ct = default)
    {
        var entry = webhookPayload["entry"]?.AsArray()?.FirstOrDefault();
        if (entry == null) return;

        var changes = entry["changes"]?.AsArray();
        if (changes == null) return;

        foreach (var change in changes)
        {
            var value = change["value"];
            if (value == null) continue;

            var field = change["field"]?.ToString();
            if (field != "messages") continue;

            var messages = value["messages"]?.AsArray();
            if (messages == null) continue;

            foreach (var msg in messages)
            {
                await ProcessMessageAsync(msg, ct);
            }
        }
    }

    /// <summary>
    /// Verify webhook signature
    /// </summary>
    public bool VerifyWebhookSignature(string signature, string payload)
    {
        // WhatsApp uses X-Hub-Signature for webhook verification
        // Implementation should compute HMAC-SHA256 and compare with signature
        // For now, return true - proper implementation needed
        return true;
    }

    private async Task ProcessMessageAsync(JsonNode message, CancellationToken ct)
    {
        var from = message?["from"]?.ToString();
        var messageId = message?["id"]?.ToString();
        var timestamp = message?["timestamp"]?.ToString();

        var textObject = message?["text"];
        var text = textObject?["body"]?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(text))
        {
            return;
        }

        var userMessage = new UserMessage(
            MessageId: messageId ?? Guid.NewGuid().ToString(),
            Timestamp: DateTimeOffset.TryParse(timestamp, out var ts) ? ts.UtcDateTime : DateTime.UtcNow,
            Platform: "whatsapp",
            UserId: from,
            Content: text
        );

        await _messageBus.PublishAsync(userMessage, ct);
    }

    private static string? ExtractPhoneNumber(AgentResponse message)
    {
        // For messages sent from the bot to the user, we need to find the original phone number
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
