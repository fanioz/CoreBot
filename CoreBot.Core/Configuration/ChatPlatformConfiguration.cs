namespace CoreBot.Core.Configuration;

/// <summary>
/// Configuration for a chat platform
/// </summary>
public class ChatPlatformConfiguration
{
    /// <summary>
    /// Whether this platform is enabled
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// API key or token for the platform. Can use environment variable syntax
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Platform-specific settings
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();
}
