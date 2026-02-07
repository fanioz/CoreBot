namespace CoreBot.Core.ChatPlatforms;

/// <summary>
/// Configuration for Telegram platform
/// </summary>
public class TelegramConfiguration
{
    /// <summary>
    /// Telegram Bot API token
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// Polling timeout in seconds (default: 30)
    /// </summary>
    public int PollingTimeout { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts for connection failures (default: 3)
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Retry delay in seconds (default: 5)
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 5;
}

/// <summary>
/// Configuration for WhatsApp platform
/// </summary>
public class WhatsAppConfiguration
{
    /// <summary>
    /// WhatsApp Business API access token
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// WhatsApp Business phone number ID
    /// </summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>
    /// Webhook verify token
    /// </summary>
    public string VerifyToken { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for WhatsApp API
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://graph.facebook.com/v18.0";

    /// <summary>
    /// Maximum retry attempts for connection failures (default: 3)
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Retry delay in seconds (default: 5)
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 5;
}

/// <summary>
/// Configuration for Feishu platform
/// </summary>
public class FeishuConfiguration
{
    /// <summary>
    /// Feishu App ID
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Feishu App Secret
    /// </summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>
    /// Encrypt key for verifying webhook requests
    /// </summary>
    public string EncryptKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for Feishu Open API
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://open.feishu.cn/open-apis";

    /// <summary>
    /// Maximum retry attempts for connection failures (default: 3)
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Retry delay in seconds (default: 5)
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 5;
}
