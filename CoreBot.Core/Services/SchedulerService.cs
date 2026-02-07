using Cronos;
using CoreBot.Core.Configuration;
using CoreBot.Core.Messaging;
using CoreBot.Core.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreBot.Core.Services;

/// <summary>
/// Scheduler service that executes tasks on a schedule using cron expressions
/// </summary>
public class SchedulerService : IHostedService
{
    private readonly IMessageBus _messageBus;
    private readonly SchedulerConfiguration _configuration;
    private readonly ILogger<SchedulerService> _logger;
    private readonly CancellationTokenSource _shutdownCts;
    private readonly Dictionary<string, ScheduledTaskRunner> _taskRunners;
    private Task? _executionTask;

    public SchedulerService(
        IMessageBus messageBus,
        IOptions<CoreBotConfiguration> configuration,
        ILogger<SchedulerService> logger)
    {
        _messageBus = messageBus;
        _configuration = configuration.Value.Scheduler;
        _logger = logger;
        _shutdownCts = new CancellationTokenSource();
        _taskRunners = new Dictionary<string, ScheduledTaskRunner>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SchedulerService starting with {TaskCount} scheduled tasks", _configuration.Tasks.Count);

        // Initialize task runners for each scheduled task
        foreach (var task in _configuration.Tasks)
        {
            try
            {
                var cronExpression = CronExpression.Parse(task.Cron, CronFormat.IncludeSeconds);
                var runner = new ScheduledTaskRunner(task, cronExpression, _logger);
                _taskRunners[task.Name] = runner;
                _logger.LogInformation("Registered scheduled task '{TaskName}' with cron expression '{CronExpression}'",
                    task.Name, task.Cron);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register scheduled task '{TaskName}': {ErrorMessage}",
                    task.Name, ex.Message);
            }
        }

        // Start the execution loop
        _executionTask = RunAsync(_shutdownCts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SchedulerService stopping");
        _shutdownCts.Cancel();

        if (_executionTask != null)
        {
            await _executionTask;
        }

        _shutdownCts.Dispose();
        _logger.LogInformation("SchedulerService stopped");
    }

    /// <summary>
    /// Main execution loop that checks for due tasks
    /// </summary>
    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Check each task
                foreach (var (taskName, runner) in _taskRunners)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    if (runner.IsDue())
                    {
                        _logger.LogInformation("Executing scheduled task '{TaskName}'", taskName);

                        // Execute the task in the background to avoid blocking other tasks
                        _ = Task.Run(() => ExecuteTaskAsync(runner.Task, ct), ct);
                    }
                }

                // Wait until the next check (every second)
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduler execution loop");
                await Task.Delay(5000, ct); // Wait before retrying
            }
        }
    }

    /// <summary>
    /// Execute a scheduled task
    /// </summary>
    private async Task ExecuteTaskAsync(Configuration.ScheduledTask task, CancellationToken ct)
    {
        using var _ = _logger.BeginScope("ScheduledTask:{TaskName}", task.Name);

        try
        {
            var action = task.Action;

            switch (action.Type.ToLower())
            {
                case "tool":
                    await ExecuteToolActionAsync(action, ct);
                    break;

                case "send_message":
                    await ExecuteSendMessageActionAsync(action, ct);
                    break;

                default:
                    _logger.LogWarning("Unknown scheduled task action type: {ActionType}", action.Type);
                    break;
            }

            _logger.LogInformation("Successfully executed scheduled task '{TaskName}'", task.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute scheduled task '{TaskName}': {ErrorMessage}",
                task.Name, ex.Message);
        }
    }

    /// <summary>
    /// Execute a tool action
    /// </summary>
    private async Task ExecuteToolActionAsync(ScheduledTaskAction action, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(action.ToolName))
        {
            _logger.LogWarning("Tool action missing tool name");
            return;
        }

        _logger.LogInformation("Executing tool '{ToolName}' with parameters: {Parameters}",
            action.ToolName,
            System.Text.Json.JsonSerializer.Serialize(action.Parameters));

        // Publish a tool execution request to the message bus
        // The AgentService or another subscriber will handle this
        var toolCall = new ToolCall(
            ToolName: action.ToolName!,
            Parameters: System.Text.Json.JsonSerializer.SerializeToElement(action.Parameters ?? new Dictionary<string, object>())
        );

        var toolCallMessage = new Messages.ToolResult(
            MessageId: Guid.NewGuid().ToString(),
            Timestamp: DateTime.UtcNow,
            ToolName: action.ToolName,
            Success: true,
            Result: $"Scheduled tool execution: {action.ToolName}"
        );

        await _messageBus.PublishAsync(toolCallMessage, ct);
    }

    /// <summary>
    /// Execute a send message action
    /// </summary>
    private async Task ExecuteSendMessageActionAsync(ScheduledTaskAction action, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(action.Platform) || string.IsNullOrEmpty(action.UserId))
        {
            _logger.LogWarning("Send message action missing platform or user ID");
            return;
        }

        if (string.IsNullOrEmpty(action.Message))
        {
            _logger.LogWarning("Send message action missing message content");
            return;
        }

        _logger.LogInformation("Sending message to {Platform}/{UserId}: {Message}",
            action.Platform, action.UserId, action.Message);

        // Publish a message to be sent to the user
        var agentResponse = new AgentResponse(
            MessageId: Guid.NewGuid().ToString(),
            Timestamp: DateTime.UtcNow,
            Platform: action.Platform!,
            UserId: action.UserId!,
            Content: action.Message!,
            ToolCalls: null
        );

        await _messageBus.PublishAsync(agentResponse, ct);
    }

    /// <summary>
    /// Internal helper class for tracking scheduled tasks
    /// </summary>
    private class ScheduledTaskRunner
    {
        public Configuration.ScheduledTask Task { get; }
        public CronExpression CronExpression { get; }
        private DateTime _nextOccurrence;

        public ScheduledTaskRunner(Configuration.ScheduledTask task, CronExpression cronExpression, ILogger logger)
        {
            Task = task;
            CronExpression = cronExpression;
            _nextOccurrence = GetNextOccurrence();
        }

        public bool IsDue()
        {
            var now = DateTime.UtcNow;
            if (now >= _nextOccurrence)
            {
                _nextOccurrence = GetNextOccurrence();
                return true;
            }
            return false;
        }

        private DateTime GetNextOccurrence()
        {
            var next = CronExpression.GetNextOccurrence(DateTime.UtcNow);
            return next ?? DateTime.UtcNow.AddYears(100); // Far future if no next occurrence
        }
    }
}
