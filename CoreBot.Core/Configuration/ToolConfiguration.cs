namespace CoreBot.Core.Configuration;

/// <summary>
/// Configuration for tool execution
/// </summary>
public class ToolConfiguration
{
    /// <summary>
    /// Path to the workspace directory for file operations
    /// </summary>
    public string WorkspacePath { get; set; } = "~/.nanobot/workspace";

    /// <summary>
    /// Maximum timeout in seconds for shell commands
    /// </summary>
    public int ShellTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// List of allowed tool names
    /// </summary>
    public List<string> AllowedTools { get; set; } = new()
    {
        "file_read",
        "file_write",
        "shell",
        "web_fetch",
        "send_message"
    };
}
