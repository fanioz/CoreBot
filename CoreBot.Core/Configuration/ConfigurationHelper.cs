using System.Text;

namespace CoreBot.Core.Configuration;

/// <summary>
/// Helper for configuration operations
/// </summary>
public static class ConfigurationHelper
{
    /// <summary>
    /// Expands environment variables in a string (e.g., ${VAR_NAME} -> value)
    /// </summary>
    public static string ExpandEnvironmentVariables(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var result = new StringBuilder();
        var i = 0;

        while (i < input.Length)
        {
            // Check for ${VAR_NAME} pattern
            if (i + 1 < input.Length && input[i] == '$' && input[i + 1] == '{')
            {
                var endIndex = input.IndexOf('}', i + 2);
                if (endIndex >= 0)
                {
                    var varName = input.Substring(i + 2, endIndex - i - 2);
                    var varValue = Environment.GetEnvironmentVariable(varName) ?? string.Empty;
                    result.Append(varValue);
                    i = endIndex + 1;
                    continue;
                }
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    /// <summary>
    /// Gets the user's corebot configuration directory
    /// </summary>
    public static string GetCorebotConfigDirectory()
    {
        // For development, use project-local .corebot directory
        var projectDir = Directory.GetCurrentDirectory();
        var corebotDir = Path.Combine(projectDir, ".corebot");

        // For production, use user home directory
        if (!Directory.Exists(corebotDir))
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".corebot");
        }

        return corebotDir;
    }

    /// <summary>
    /// Gets the path to the user's corebot configuration file
    /// </summary>
    public static string GetCorebotConfigPath()
    {
        // For development, use project-local .corebot/config.json
        var projectDir = Directory.GetCurrentDirectory();
        var projectConfig = Path.Combine(projectDir, ".corebot", "config.json");

        // For production, use user home directory
        if (!File.Exists(projectConfig))
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".corebot", "config.json");
        }

        return projectConfig;
    }

    /// <summary>
    /// Creates a default configuration file at the specified path
    /// </summary>
    public static async Task CreateDefaultConfigurationAsync(string path)
    {
        var defaultConfig = new CoreBotConfiguration
        {
            Llm = new LlmConfiguration
            {
                Provider = "openrouter",
                ApiKey = "${OPENROUTER_API_KEY}",
                Model = "anthropic/claude-3.5-sonnet",
                MaxTokens = 4096,
                Temperature = 0.7
            },
            ChatPlatforms = new Dictionary<string, ChatPlatformConfiguration>
            {
                ["telegram"] = new ChatPlatformConfiguration
                {
                    Enabled = true,
                    ApiKey = "${TELEGRAM_BOT_TOKEN}",
                    Settings = new Dictionary<string, string>()
                },
                ["whatsapp"] = new ChatPlatformConfiguration
                {
                    Enabled = false,
                    ApiKey = "${WHATSAPP_API_KEY}",
                    Settings = new Dictionary<string, string>()
                },
                ["feishu"] = new ChatPlatformConfiguration
                {
                    Enabled = false,
                    ApiKey = "${FEISHU_API_KEY}",
                    Settings = new Dictionary<string, string>()
                }
            },
            Tools = new ToolConfiguration
            {
                WorkspacePath = "~/.corebot/workspace",
                ShellTimeoutSeconds = 30,
                AllowedTools = new List<string> { "file_read", "file_write", "shell", "web_fetch", "send_message" }
            },
            Scheduler = new SchedulerConfiguration
            {
                Tasks = new List<ScheduledTask>()
            },
            Logging = new LoggingConfiguration
            {
                Level = "Information"
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(defaultConfig, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, json);
    }
}
