using System.Text.Json;
using CoreBot.Core.Configuration;
using CoreBot.Core.Memory;
using CoreBot.Core.Messaging;
using CoreBot.Core.Messages;
using CoreBot.Core.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreBot.Core.Subagents;

/// <summary>
/// Manages subagents for long-running background tasks
/// </summary>
public class SubagentManager : IHostedService
{
    private readonly IMessageBus _messageBus;
    private readonly IMemoryStore _memoryStore;
    private readonly ToolRegistry _toolRegistry;
    private readonly CoreBotConfiguration _configuration;
    private readonly ILogger<SubagentManager> _logger;
    private readonly Dictionary<string, Subagent> _runningSubagents;
    private readonly CancellationTokenSource _shutdownCts;
    private Task? _processingTask;

    public SubagentManager(
        IMessageBus messageBus,
        IMemoryStore memoryStore,
        ToolRegistry toolRegistry,
        IOptions<CoreBotConfiguration> configuration,
        ILogger<SubagentManager> logger)
    {
        _messageBus = messageBus;
        _memoryStore = memoryStore;
        _toolRegistry = toolRegistry;
        _configuration = configuration.Value;
        _logger = logger;
        _runningSubagents = new Dictionary<string, Subagent>();
        _shutdownCts = new CancellationTokenSource();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SubagentManager starting");
        _processingTask = RunAsync(_shutdownCts.Token);

        // Resume any incomplete subagents from memory
        _ = Task.Run(() => ResumeSubagentsAsync(_shutdownCts.Token), _shutdownCts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SubagentManager stopping");
        _shutdownCts.Cancel();

        // Cancel all running subagents
        foreach (var (id, subagent) in _runningSubagents)
        {
            if (subagent.State == SubagentState.Running)
            {
                await CancelSubagentAsync(id, cancellationToken);
            }
        }

        if (_processingTask != null)
        {
            await _processingTask;
        }

        _shutdownCts.Dispose();
        _logger.LogInformation("SubagentManager stopped");
    }

    /// <summary>
    /// Create a new subagent for a long-running task
    /// </summary>
    public async Task<Subagent> CreateSubagentAsync(
        string name,
        string taskName,
        JsonElement? parameters,
        UserMessage triggerMessage,
        CancellationToken ct = default)
    {
        var subagent = new Subagent
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            State = SubagentState.Created,
            Platform = triggerMessage.Platform,
            UserId = triggerMessage.UserId,
            TriggerMessage = triggerMessage,
            TaskName = taskName,
            TaskParameters = parameters,
            StatusMessage = "Created",
            CreatedAt = DateTime.UtcNow
        };

        _runningSubagents[subagent.Id] = subagent;

        // Persist to memory
        await PersistSubagentAsync(subagent, ct);

        _logger.LogInformation("Created subagent {SubagentId} '{SubagentName}' for task {TaskName}",
            subagent.Id, subagent.Name, taskName);

        // Start the subagent in the background
        _ = Task.Run(() => RunSubagentAsync(subagent, _shutdownCts.Token), _shutdownCts.Token);

        return subagent;
    }

    /// <summary>
    /// Get a subagent by ID
    /// </summary>
    public Subagent? GetSubagent(string id)
    {
        return _runningSubagents.TryGetValue(id, out var subagent) ? subagent : null;
    }

    /// <summary>
    /// Get all subagents for a user
    /// </summary>
    public List<Subagent> GetUserSubagents(string platform, string userId)
    {
        return _runningSubagents.Values
            .Where(s => s.Platform == platform && s.UserId == userId)
            .ToList();
    }

    /// <summary>
    /// Cancel a running subagent
    /// </summary>
    public async Task<bool> CancelSubagentAsync(string subagentId, CancellationToken ct = default)
    {
        if (!_runningSubagents.TryGetValue(subagentId, out var subagent))
        {
            _logger.LogWarning("Cannot cancel subagent {SubagentId}: not found", subagentId);
            return false;
        }

        if (subagent.State != SubagentState.Running)
        {
            _logger.LogWarning("Cannot cancel subagent {SubagentId}: not running (state: {State})",
                subagentId, subagent.State);
            return false;
        }

        _logger.LogInformation("Cancelling subagent {SubagentId} '{SubagentName}'",
            subagentId, subagent.Name);

        subagent.State = SubagentState.Cancelled;
        subagent.CompletedAt = DateTime.UtcNow;
        subagent.StatusMessage = "Cancelled by user";

        await PersistSubagentAsync(subagent, ct);

        // Publish completion notification
        await PublishCompletionNotificationAsync(subagent, ct);

        _runningSubagents.Remove(subagentId);

        return true;
    }

    /// <summary>
    /// Main processing loop
    /// </summary>
    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    /// <summary>
    /// Run a subagent's task
    /// </summary>
    private async Task RunSubagentAsync(Subagent subagent, CancellationToken ct)
    {
        using var _ = _logger.BeginScope("Subagent:{SubagentId}:{SubagentName}",
            subagent.Id, subagent.Name);

        try
        {
            _logger.LogInformation("Starting subagent");

            subagent.State = SubagentState.Running;
            subagent.StartedAt = DateTime.UtcNow;
            subagent.StatusMessage = "Running";
            await PersistSubagentAsync(subagent, ct);

            // Execute the task
            var result = await _toolRegistry.ExecuteAsync(
                subagent.TaskName,
                subagent.TaskParameters ?? JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
                ct
            );

            if (result.Success)
            {
                subagent.State = SubagentState.Completed;
                subagent.Result = result.Result;
                subagent.StatusMessage = "Completed successfully";
                subagent.Progress = 100;

                _logger.LogInformation("Subagent completed successfully: {Result}", result.Result);
            }
            else
            {
                subagent.State = SubagentState.Failed;
                subagent.Error = result.Error;
                subagent.StatusMessage = $"Failed: {result.Error}";

                _logger.LogError("Subagent failed: {Error}", result.Error);
            }
        }
        catch (OperationCanceledException)
        {
            subagent.State = SubagentState.Cancelled;
            subagent.StatusMessage = "Cancelled";
            _logger.LogInformation("Subagent was cancelled");
        }
        catch (Exception ex)
        {
            subagent.State = SubagentState.Failed;
            subagent.Error = ex.Message;
            subagent.StatusMessage = $"Failed: {ex.Message}";
            _logger.LogError(ex, "Subagent failed with exception");
        }
        finally
        {
            subagent.CompletedAt = DateTime.UtcNow;
            await PersistSubagentAsync(subagent, ct);

            // Publish completion notification
            await PublishCompletionNotificationAsync(subagent, ct);

            _runningSubagents.Remove(subagent.Id);
        }
    }

    /// <summary>
    /// Persist subagent state to disk
    /// </summary>
    private async Task PersistSubagentAsync(Subagent subagent, CancellationToken ct)
    {
        try
        {
            var fileName = $"subagent_{subagent.Id}.json";
            var directory = Path.Combine(_configuration.DataDirectory, "subagents");

            // Ensure directory exists
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var filePath = Path.Combine(directory, fileName);
            var json = subagent.ToJson();

            await File.WriteAllTextAsync(filePath, json, ct);

            _logger.LogDebug("Persisted subagent {SubagentId} state: {State}",
                subagent.Id, subagent.State);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist subagent {SubagentId}", subagent.Id);
        }
    }

    /// <summary>
    /// Resume incomplete subagents from disk on startup
    /// </summary>
    private async Task ResumeSubagentsAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Resuming incomplete subagents from disk");

            var directory = Path.Combine(_configuration.DataDirectory, "subagents");

            if (!Directory.Exists(directory))
            {
                _logger.LogInformation("No subagents directory found");
                return;
            }

            var files = Directory.GetFiles(directory, "subagent_*.json");
            _logger.LogInformation("Found {Count} persisted subagent files", files.Length);

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var subagent = Subagent.FromJson(json);

                    if (subagent != null && subagent.State == SubagentState.Running)
                    {
                        _logger.LogWarning("Subagent {SubagentId} was in Running state at shutdown, marking as failed",
                            subagent.Id);
                        subagent.State = SubagentState.Failed;
                        subagent.Error = "Interrupted by system shutdown";
                        subagent.CompletedAt = DateTime.UtcNow;

                        // Publish failure notification
                        await PublishCompletionNotificationAsync(subagent, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load subagent from file {File}", file);
                }
            }

            _logger.LogInformation("Subagent resume complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming subagents");
        }
    }

    /// <summary>
    /// Publish completion notification for a subagent
    /// </summary>
    private async Task PublishCompletionNotificationAsync(Subagent subagent, CancellationToken ct)
    {
        try
        {
            var message = new SubagentCompletedMessage
            {
                Subagent = subagent,
                Platform = subagent.Platform,
                UserId = subagent.UserId
            };

            await _messageBus.PublishAsync(message, ct);

            _logger.LogInformation("Published completion notification for subagent {SubagentId} to {Platform}/{UserId}",
                subagent.Id, subagent.Platform, subagent.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish completion notification for subagent {SubagentId}",
                subagent.Id);
        }
    }
}
