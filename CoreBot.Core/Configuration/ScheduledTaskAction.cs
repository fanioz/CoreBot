namespace CoreBot.Core.Configuration;

/// <summary>
/// Action for a scheduled task
/// </summary>
public class ScheduledTaskAction
{
    /// <summary>
    /// Type of action ("tool" or "send_message")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Platform for message actions
    /// </summary>
    public string? Platform { get; set; }

    /// <summary>
    /// User ID for message actions
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Message content for message actions
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Tool name for tool actions
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Tool parameters as JSON
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// A scheduled task
/// </summary>
public class ScheduledTask
{
    /// <summary>
    /// Task name for identification
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Cron expression for scheduling
    /// </summary>
    public string Cron { get; set; } = string.Empty;

    /// <summary>
    /// Action to execute when triggered
    /// </summary>
    public ScheduledTaskAction Action { get; set; } = new();
}

/// <summary>
/// Configuration for the scheduler
/// </summary>
public class SchedulerConfiguration
{
    /// <summary>
    /// List of scheduled tasks
    /// </summary>
    public List<ScheduledTask> Tasks { get; set; } = new();
}
