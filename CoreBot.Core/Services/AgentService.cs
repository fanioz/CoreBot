using System.Text.Json;
using CoreBot.Core.Configuration;
using CoreBot.Core.LLM;
using CoreBot.Core.Memory;
using CoreBot.Core.Messaging;
using CoreBot.Core.Messages;
using CoreBot.Core.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreBot.Core.Services;

/// <summary>
/// Agent service that orchestrates LLM interactions and tool execution
/// </summary>
public class AgentService : IHostedService
{
    private readonly IMessageBus _messageBus;
    private readonly IMemoryStore _memoryStore;
    private readonly ILlmProvider _llmProvider;
    private readonly ToolRegistry _toolRegistry;
    private readonly CoreBotConfiguration _configuration;
    private readonly ILogger<AgentService> _logger;
    private readonly CancellationTokenSource _shutdownCts;
    private Task? _processingTask;

    public AgentService(
        IMessageBus messageBus,
        IMemoryStore memoryStore,
        ILlmProvider llmProvider,
        ToolRegistry toolRegistry,
        IOptions<CoreBotConfiguration> configuration,
        ILogger<AgentService> logger)
    {
        _messageBus = messageBus;
        _memoryStore = memoryStore;
        _llmProvider = llmProvider;
        _toolRegistry = toolRegistry;
        _configuration = configuration.Value;
        _logger = logger;
        _shutdownCts = new CancellationTokenSource();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AgentService starting");
        _processingTask = ProcessMessagesAsync(_shutdownCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AgentService stopping");
        _shutdownCts.Cancel();

        if (_processingTask != null)
        {
            await _processingTask;
        }

        _shutdownCts.Dispose();
        _logger.LogInformation("AgentService stopped");
    }

    /// <summary>
    /// Main message processing loop
    /// </summary>
    private async Task ProcessMessagesAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var message in _messageBus.SubscribeAsync<UserMessage>(ct))
            {
                try
                {
                    await ProcessUserMessageAsync(message, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing user message {MessageId}", message.MessageId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    /// <summary>
    /// Process a user message through the agent
    /// </summary>
    private async Task ProcessUserMessageAsync(UserMessage userMessage, CancellationToken ct)
    {
        using var _ = _logger.BeginScope("ProcessMessage:{MessageId}:{Platform}:{UserId}",
            userMessage.MessageId, userMessage.Platform, userMessage.UserId);

        _logger.LogInformation("Processing user message");

        // Save user message to memory
        var storedUserMessage = new StoredMessage
        {
            Timestamp = userMessage.Timestamp,
            Role = "user",
            Content = userMessage.Content
        };
        await _memoryStore.SaveMessageAsync(userMessage.Platform, userMessage.UserId, storedUserMessage, ct);

        // Get conversation ID
        var conversationId = await _memoryStore.GetOrCreateConversationIdAsync(userMessage.Platform, userMessage.UserId, ct);

        // Load conversation history
        var history = await _memoryStore.GetHistoryAsync(userMessage.Platform, userMessage.UserId, limit: 50, ct);

        // Build LLM request with conversation context
        var llmRequest = await BuildLlmRequestAsync(history, userMessage, ct);

        // Process the request with tool calling loop
        var (finalResponse, toolResults) = await ProcessWithToolCallingAsync(llmRequest, userMessage, ct);

        // Publish agent response
        var agentResponse = new AgentResponse(
            MessageId: Guid.NewGuid().ToString(),
            Timestamp: DateTime.UtcNow,
            Platform: userMessage.Platform,
            UserId: userMessage.UserId,
            Content: finalResponse.Content,
            ToolCalls: finalResponse.ToolCalls
        );

        await _messageBus.PublishAsync(agentResponse, ct);

        // Save assistant response to memory
        var storedAssistantMessage = new StoredMessage
        {
            Timestamp = agentResponse.Timestamp,
            Role = "assistant",
            Content = agentResponse.Content
        };
        await _memoryStore.SaveMessageAsync(userMessage.Platform, userMessage.UserId, storedAssistantMessage, ct);

        // Save tool results to memory
        foreach (var toolResult in toolResults)
        {
            var storedToolMessage = new StoredMessage
            {
                Timestamp = toolResult.Timestamp,
                Role = "tool",
                ToolName = toolResult.ToolName,
                Result = toolResult.Result
            };
            await _memoryStore.SaveMessageAsync(userMessage.Platform, userMessage.UserId, storedToolMessage, ct);
        }
    }

    /// <summary>
    /// Build LLM request with conversation history and tool definitions
    /// </summary>
    private async Task<LlmRequest> BuildLlmRequestAsync(List<StoredMessage> history, UserMessage userMessage, CancellationToken ct)
    {
        var messages = new List<LlmMessage>();

        // Add system prompt
        messages.Add(new LlmMessage
        {
            Role = "system",
            Content = _configuration.Llm.SystemPrompt ?? "You are a helpful AI assistant."
        });

        // Add conversation history (excluding the current message which will be added separately)
        var historyLimit = Math.Min(history.Count, 20); // Limit context window
        for (int i = 0; i < history.Count - 1; i++) // Exclude last message as it's the current one
        {
            var msg = history[i];
            messages.Add(new LlmMessage
            {
                Role = msg.Role,
                Content = msg.Content
            });
        }

        // Add current user message
        messages.Add(new LlmMessage
        {
            Role = "user",
            Content = userMessage.Content
        });

        // Get tool definitions
        var tools = GetToolDefinitions();

        return new LlmRequest
        {
            Messages = messages,
            Tools = tools,
            MaxTokens = _configuration.Llm.MaxTokens,
            Temperature = _configuration.Llm.Temperature
        };
    }

    /// <summary>
    /// Get tool definitions for LLM function calling
    /// </summary>
    private List<ToolDefinition>? GetToolDefinitions()
    {
        if (!_configuration.Llm.EnableToolCalling)
        {
            return null;
        }

        return _toolRegistry.GetAllTools()
            .Select(tool => new ToolDefinition
            {
                Name = tool.Name,
                Description = tool.Description,
                Parameters = tool.GetSchema()
            })
            .ToList();
    }

    /// <summary>
    /// Process LLM request with tool calling loop
    /// </summary>
    private async Task<(LlmResponse FinalResponse, List<Messages.ToolResult> ToolResults)> ProcessWithToolCallingAsync(
        LlmRequest request,
        UserMessage userMessage,
        CancellationToken ct)
    {
        var allToolResults = new List<Messages.ToolResult>();
        var maxIterations = 10; // Prevent infinite loops
        var currentRequest = request;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            // Get LLM response
            var response = await _llmProvider.CompleteAsync(currentRequest, ct);

            if (!string.IsNullOrEmpty(response.Error))
            {
                _logger.LogError("LLM request failed: {Error}", response.Error);
                return (response, allToolResults);
            }

            // If no tool calls, we're done
            if (response.ToolCalls == null || response.ToolCalls.Count == 0)
            {
                return (response, allToolResults);
            }

            _logger.LogInformation("LLM requested {Count} tool calls", response.ToolCalls.Count);

            // Execute tools
            foreach (var toolCall in response.ToolCalls)
            {
                try
                {
                    var toolResult = await _toolRegistry.ExecuteAsync(toolCall.ToolName, toolCall.Parameters, ct);
                    var toolResultMessage = new Messages.ToolResult(
                        MessageId: Guid.NewGuid().ToString(),
                        Timestamp: DateTime.UtcNow,
                        ToolName: toolCall.ToolName,
                        Success: toolResult.Success,
                        Result: toolResult.Success ? toolResult.Result : $"Error: {toolResult.Error}"
                    );
                    allToolResults.Add(toolResultMessage);

                    // Add tool result to conversation
                    currentRequest.Messages.Add(new LlmMessage
                    {
                        Role = "assistant",
                        Content = response.Content,
                        ToolCalls = new List<ToolCall> { toolCall }
                    });

                    currentRequest.Messages.Add(new LlmMessage
                    {
                        Role = "tool",
                        Content = toolResultMessage.Result,
                        ToolResult = toolResultMessage
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.ToolName);
                    var errorResult = new Messages.ToolResult(
                        MessageId: Guid.NewGuid().ToString(),
                        Timestamp: DateTime.UtcNow,
                        ToolName: toolCall.ToolName,
                        Success: false,
                        Result: $"Error: {ex.Message}"
                    );
                    allToolResults.Add(errorResult);
                }
            }

            // Continue loop to get final response with tool results
        }

        _logger.LogWarning("Tool calling loop exceeded max iterations ({MaxIterations})", maxIterations);

        // Get one final response after max iterations
        var finalResponse = await _llmProvider.CompleteAsync(currentRequest, ct);
        return (finalResponse, allToolResults);
    }
}
