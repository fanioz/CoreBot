namespace CoreBot.Core.Configuration;

/// <summary>
/// Configuration for LLM provider settings
/// </summary>
public class LlmConfiguration
{
    /// <summary>
    /// The LLM provider to use (e.g., "openrouter", "anthropic", "openai", "deepseek", "groq", "gemini")
    /// </summary>
    public string Provider { get; set; } = "openrouter";

    /// <summary>
    /// API key for the LLM provider. Can use environment variable syntax like ${API_KEY}
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The model to use (e.g., "anthropic/claude-3-5-sonnet", "gpt-4")
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Maximum tokens for completion
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Temperature for generation (0.0 to 1.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// System prompt to use for conversations
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Whether to enable tool calling (function calling)
    /// </summary>
    public bool EnableToolCalling { get; set; } = true;
}
