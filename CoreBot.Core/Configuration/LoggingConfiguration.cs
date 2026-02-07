namespace CoreBot.Core.Configuration;

/// <summary>
/// Configuration for logging
/// </summary>
public class LoggingConfiguration
{
    /// <summary>
    /// Log level (Debug, Information, Warning, Error, Critical)
    /// </summary>
    public string Level { get; set; } = "Information";

    /// <summary>
    /// Whether to enable log scopes for correlation
    /// </summary>
    public bool EnableScopes { get; set; } = true;

    /// <summary>
    /// Whether to log to console
    /// </summary>
    public bool LogToConsole { get; set; } = true;

    /// <summary>
    /// Whether to log to Windows Event Log (Windows Service only)
    /// </summary>
    public bool LogToWindowsEventLog { get; set; } = false;

    /// <summary>
    /// Windows Event Log source name
    /// </summary>
    public string WindowsEventLogSource { get; set; } = "CoreBot";

    /// <summary>
    /// Windows Event Log name
    /// </summary>
    public string WindowsEventLogName { get; set; } = "Application";
}
