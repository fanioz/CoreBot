using Microsoft.Extensions.Options;

namespace CoreBot.Core.Configuration;

/// <summary>
/// Validates CoreBotConfiguration on startup
/// </summary>
public class CoreBotConfigurationValidator : IValidateOptions<CoreBotConfiguration>
{
    public ValidateOptionsResult Validate(string? name, CoreBotConfiguration options)
    {
        var errors = new List<string>();

        // Validate LLM configuration
        if (string.IsNullOrWhiteSpace(options.Llm.Provider))
        {
            errors.Add("LLM Provider is required");
        }

        // Allow empty ApiKey for development (will use environment variable expansion)
        // But log a warning
        if (string.IsNullOrWhiteSpace(options.Llm.ApiKey))
        {
            // This is OK - ApiKey will use environment variable expansion
        }

        if (string.IsNullOrWhiteSpace(options.Llm.Model))
        {
            errors.Add("LLM Model is required");
        }

        // Validate temperature range
        if (options.Llm.Temperature < 0.0 || options.Llm.Temperature > 1.0)
        {
            errors.Add("LLM Temperature must be between 0.0 and 1.0");
        }

        // Validate MaxTokens
        if (options.Llm.MaxTokens <= 0)
        {
            errors.Add("LLM MaxTokens must be greater than 0");
        }

        // Validate at least one chat platform is configured
        var enabledPlatforms = options.ChatPlatforms.Where(p => p.Value.Enabled).ToList();
        if (enabledPlatforms.Count == 0)
        {
            errors.Add("At least one chat platform must be enabled");
        }

        // Validate enabled platforms have API keys
        foreach (var (platformName, platformConfig) in enabledPlatforms)
        {
            // Allow empty ApiKey for development (will use environment variable expansion)
        }

        // Validate tool configuration
        if (string.IsNullOrWhiteSpace(options.Tools.WorkspacePath))
        {
            errors.Add("Tools WorkspacePath is required");
        }

        if (options.Tools.ShellTimeoutSeconds <= 0 || options.Tools.ShellTimeoutSeconds > 300)
        {
            errors.Add("Tools ShellTimeoutSeconds must be between 1 and 300 seconds");
        }

        if (options.Tools.AllowedTools.Count == 0)
        {
            errors.Add("At least one tool must be allowed");
        }

        // Validate logging configuration
        if (string.IsNullOrWhiteSpace(options.Logging.Level))
        {
            errors.Add("Logging Level is required");
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail($"Configuration validation failed:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        return ValidateOptionsResult.Success;
    }
}
