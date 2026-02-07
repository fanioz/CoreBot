namespace CoreBot.Core.Configuration;

/// <summary>
/// Root configuration for CoreBot
/// </summary>
public class CoreBotConfiguration
{
    /// <summary>
    /// LLM provider configuration
    /// </summary>
    public LlmConfiguration Llm { get; set; } = new();

    /// <summary>
    /// Chat platform configurations keyed by platform name (telegram, whatsapp, feishu)
    /// </summary>
    public Dictionary<string, ChatPlatformConfiguration> ChatPlatforms { get; set; } = new()
    {
        ["telegram"] = new(),
        ["whatsapp"] = new(),
        ["feishu"] = new()
    };

    /// <summary>
    /// Tool execution configuration
    /// </summary>
    public ToolConfiguration Tools { get; set; } = new();

    /// <summary>
    /// Scheduler configuration
    /// </summary>
    public SchedulerConfiguration Scheduler { get; set; } = new();

    /// <summary>
    /// Logging configuration
    /// </summary>
    public LoggingConfiguration Logging { get; set; } = new();

    /// <summary>
    /// Data directory for persistent storage
    /// </summary>
    public string DataDirectory { get; set; } = "./data";

    /// <summary>
    /// Skills plugin configuration
    /// </summary>
    public SkillsConfiguration Skills { get; set; } = new();
}
