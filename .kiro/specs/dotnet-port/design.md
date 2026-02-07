# Design Document: Nanobot .NET Port

## Overview

This design describes a .NET 8+ implementation of Nanobot that maintains the ultra-lightweight philosophy (<6,000 lines) while adding native Windows Service and systemd daemon support. The architecture leverages .NET's Generic Host pattern, dependency injection, and async/await for cross-platform service hosting.

The design preserves Nanobot's core event-driven message bus architecture while using .NET-native patterns: IHostedService for background services, IOptions for configuration, System.Text.Json for serialization, and HttpClient for web access.

## Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Service Host Layer                        │
│  (Windows Service / systemd daemon via Generic Host)         │
└─────────────────────────────────────────────────────────────┘
                            │
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Message    │  │     Agent    │  │   Scheduler  │      │
│  │     Bus      │  │   Service    │  │   Service    │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                            │
┌─────────────────────────────────────────────────────────────┐
│                    Integration Layer                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ Chat Platform│  │ LLM Provider │  │     Tool     │      │
│  │  Adapters    │  │   Clients    │  │   Registry   │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                            │
┌─────────────────────────────────────────────────────────────┐
│                    Storage Layer                             │
│  ┌──────────────┐  ┌──────────────┐                         │
│  │    Memory    │  │Configuration │                         │
│  │    Store     │  │    Store     │                         │
│  └──────────────┘  └──────────────┘                         │
└─────────────────────────────────────────────────────────────┘
```

### Service Host Layer

The Service Host Layer uses .NET Generic Host (`Microsoft.Extensions.Hosting`) which provides:
- Cross-platform service lifetime management
- Built-in dependency injection
- Configuration system
- Logging infrastructure
- Graceful shutdown handling

**Windows Service Support:**
- Use `Microsoft.Extensions.Hosting.WindowsServices` package
- Call `UseWindowsService()` on HostBuilder
- Integrates with Windows Service Control Manager (SCM)
- Supports service installation via `sc.exe` or PowerShell

**systemd Daemon Support:**
- Use `Microsoft.Extensions.Hosting.Systemd` package
- Call `UseSystemd()` on HostBuilder
- Integrates with systemd lifecycle notifications
- Logs to stdout for journald capture

### Application Layer

**Message Bus:**
- Central event routing using `System.Threading.Channels` for async message passing
- Message types: UserMessage, AgentResponse, ToolCall, ToolResult, SystemEvent
- Pub/sub pattern: components subscribe to message types
- Backpressure handling via bounded channels

**Agent Service:**
- IHostedService implementation that processes messages
- Maintains conversation context
- Orchestrates LLM calls and tool execution
- Manages subagent lifecycle

**Scheduler Service:**
- IHostedService implementation using `System.Threading.PeriodicTimer`
- Parses cron expressions using `Cronos` library (minimal dependency)
- Triggers scheduled tasks by publishing messages to the bus

### Integration Layer

**Chat Platform Adapters:**
- Interface: `IChatPlatform` with methods: `ConnectAsync()`, `SendMessageAsync()`, `DisconnectAsync()`
- Implementations: TelegramAdapter, WhatsAppAdapter, FeishuAdapter
- Each adapter runs as an IHostedService
- Uses platform-specific SDKs or HTTP APIs
- Publishes received messages to Message Bus

**LLM Provider Clients:**
- Interface: `ILlmProvider` with methods: `CompleteAsync()`, `StreamCompleteAsync()`
- Implementations: OpenRouterClient, AnthropicClient, OpenAIClient, etc.
- Uses HttpClient with typed clients pattern
- Supports function calling via provider-specific JSON schemas
- Handles streaming responses via IAsyncEnumerable

**Tool Registry:**
- Dictionary-based registry: `Dictionary<string, IToolDefinition>`
- Interface: `IToolDefinition` with methods: `ExecuteAsync()`, `GetSchema()`
- Built-in tools: FileReadTool, FileWriteTool, ShellTool, WebFetchTool, SendMessageTool
- Validates parameters against JSON schema
- Enforces workspace sandboxing for file operations
- Enforces timeouts for shell commands

### Storage Layer

**Memory Store:**
- File-based storage using JSON
- Directory structure: `~/.nanobot/memory/{platform}/{user_id}/`
- Each conversation stored as `{conversation_id}.json`
- Uses `System.Text.Json` for serialization
- Implements simple indexing for queries

**Configuration Store:**
- Uses `Microsoft.Extensions.Configuration`
- Loads from: `appsettings.json`, `~/.nanobot/config.json`, environment variables
- Binds to strongly-typed configuration classes using IOptions pattern
- Validates configuration on startup

## Components and Interfaces

### Core Interfaces

```csharp
// Message Bus
public interface IMessageBus
{
    ValueTask PublishAsync<T>(T message, CancellationToken ct = default) where T : IMessage;
    IAsyncEnumerable<T> SubscribeAsync<T>(CancellationToken ct = default) where T : IMessage;
}

// Chat Platform
public interface IChatPlatform
{
    string PlatformName { get; }
    Task ConnectAsync(CancellationToken ct);
    Task SendMessageAsync(string userId, string message, CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
}

// LLM Provider
public interface ILlmProvider
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct);
    IAsyncEnumerable<LlmStreamChunk> StreamCompleteAsync(LlmRequest request, CancellationToken ct);
}

// Tool Definition
public interface IToolDefinition
{
    string Name { get; }
    string Description { get; }
    JsonDocument GetSchema();
    Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct);
}

// Memory Store
public interface IMemoryStore
{
    Task SaveMessageAsync(ConversationMessage message, CancellationToken ct);
    Task<List<ConversationMessage>> GetHistoryAsync(string platform, string userId, int limit, CancellationToken ct);
}
```

### Message Types

```csharp
public interface IMessage
{
    string MessageId { get; }
    DateTime Timestamp { get; }
}

public record UserMessage(
    string MessageId,
    DateTime Timestamp,
    string Platform,
    string UserId,
    string Content
) : IMessage;

public record AgentResponse(
    string MessageId,
    DateTime Timestamp,
    string Platform,
    string UserId,
    string Content,
    List<ToolCall>? ToolCalls
) : IMessage;

public record ToolCall(
    string ToolName,
    JsonElement Parameters
);

public record ToolResult(
    string MessageId,
    DateTime Timestamp,
    string ToolName,
    bool Success,
    string Result
) : IMessage;
```

### Configuration Models

```csharp
public class NanobotConfiguration
{
    public LlmConfiguration Llm { get; set; }
    public Dictionary<string, ChatPlatformConfiguration> ChatPlatforms { get; set; }
    public ToolConfiguration Tools { get; set; }
    public SchedulerConfiguration Scheduler { get; set; }
    public LoggingConfiguration Logging { get; set; }
}

public class LlmConfiguration
{
    public string Provider { get; set; } // "openrouter", "anthropic", "openai", etc.
    public string ApiKey { get; set; }
    public string Model { get; set; }
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.7;
}

public class ChatPlatformConfiguration
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; }
    public Dictionary<string, string> Settings { get; set; }
}

public class ToolConfiguration
{
    public string WorkspacePath { get; set; }
    public int ShellTimeoutSeconds { get; set; } = 30;
    public List<string> AllowedTools { get; set; }
}
```

### Hosted Services

```csharp
// Agent Service - processes messages and orchestrates LLM + tools
public class AgentService : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly ILlmProvider _llmProvider;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMemoryStore _memoryStore;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var message in _messageBus.SubscribeAsync<UserMessage>(ct))
        {
            await ProcessUserMessageAsync(message, ct);
        }
    }

    private async Task ProcessUserMessageAsync(UserMessage message, CancellationToken ct)
    {
        // 1. Load conversation history
        // 2. Build LLM request with history + tools
        // 3. Call LLM
        // 4. If tool calls, execute tools and continue
        // 5. Publish AgentResponse to message bus
        // 6. Save to memory store
    }
}

// Chat Platform Service - connects to platform and routes messages
public class ChatPlatformService : BackgroundService
{
    private readonly IChatPlatform _platform;
    private readonly IMessageBus _messageBus;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _platform.ConnectAsync(ct);
        
        // Subscribe to AgentResponse messages for this platform
        await foreach (var response in _messageBus.SubscribeAsync<AgentResponse>(ct))
        {
            if (response.Platform == _platform.PlatformName)
            {
                await _platform.SendMessageAsync(response.UserId, response.Content, ct);
            }
        }
    }
}

// Scheduler Service - triggers scheduled tasks
public class SchedulerService : BackgroundService
{
    private readonly IMessageBus _messageBus;
    private readonly List<ScheduledTask> _tasks;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        
        while (await timer.WaitForNextTickAsync(ct))
        {
            var now = DateTime.UtcNow;
            foreach (var task in _tasks.Where(t => t.ShouldRun(now)))
            {
                await _messageBus.PublishAsync(task.CreateMessage(), ct);
            }
        }
    }
}
```

## Data Models

### Conversation Storage

```json
{
  "conversationId": "telegram_12345_20240101",
  "platform": "telegram",
  "userId": "12345",
  "messages": [
    {
      "role": "user",
      "content": "What's the weather?",
      "timestamp": "2024-01-01T10:00:00Z"
    },
    {
      "role": "assistant",
      "content": "I'll check the weather for you.",
      "toolCalls": [
        {
          "toolName": "web_fetch",
          "parameters": { "url": "https://api.weather.com/..." }
        }
      ],
      "timestamp": "2024-01-01T10:00:01Z"
    },
    {
      "role": "tool",
      "toolName": "web_fetch",
      "result": "{\"temperature\": 72, \"condition\": \"sunny\"}",
      "timestamp": "2024-01-01T10:00:02Z"
    },
    {
      "role": "assistant",
      "content": "It's 72°F and sunny!",
      "timestamp": "2024-01-01T10:00:03Z"
    }
  ]
}
```

### Configuration File

```json
{
  "llm": {
    "provider": "openrouter",
    "apiKey": "${OPENROUTER_API_KEY}",
    "model": "anthropic/claude-3-5-sonnet",
    "maxTokens": 4096,
    "temperature": 0.7
  },
  "chatPlatforms": {
    "telegram": {
      "enabled": true,
      "apiKey": "${TELEGRAM_BOT_TOKEN}",
      "settings": {}
    },
    "whatsapp": {
      "enabled": false,
      "apiKey": "${WHATSAPP_API_KEY}",
      "settings": {}
    }
  },
  "tools": {
    "workspacePath": "~/.nanobot/workspace",
    "shellTimeoutSeconds": 30,
    "allowedTools": ["file_read", "file_write", "shell", "web_fetch", "send_message"]
  },
  "scheduler": {
    "tasks": [
      {
        "name": "daily_summary",
        "cron": "0 9 * * *",
        "action": {
          "type": "send_message",
          "platform": "telegram",
          "userId": "12345",
          "message": "Good morning! Here's your daily summary."
        }
      }
    ]
  },
  "logging": {
    "level": "Information"
  }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*


### Core System Properties

Property 1: Graceful Shutdown Persistence
*For any* running Nanobot instance with active connections and state, when a shutdown signal is received, all connections should be closed and all state should be persisted to disk before the process terminates.
**Validates: Requirements 1.4**

Property 2: Message Bus Routing to Handlers
*For any* message arriving from any Chat_Platform, the Message_Bus should route that message to the appropriate handler based on message type and platform.
**Validates: Requirements 3.4**

Property 3: Response Routing to Origin Platform
*For any* agent response generated for a user message, the Message_Bus should deliver that response to the same Chat_Platform from which the original message came.
**Validates: Requirements 3.5**

Property 4: Connection Failure Recovery
*For any* Chat_Platform connection failure, the system should log the error and attempt reconnection according to the configured retry policy.
**Validates: Requirements 3.6**

Property 5: Provider Configuration Consistency
*For any* configured LLM_Provider and any sequence of completion requests, all completions should use the configured provider without switching to a different provider.
**Validates: Requirements 4.7**

Property 6: LLM Error Handling
*For any* LLM_Provider request that fails (network error, API error, timeout), the system should return a descriptive error message that includes the failure reason.
**Validates: Requirements 4.8**

Property 7: Tool Execution on Request
*For any* valid tool call request from an LLM_Provider with valid parameters, the Tool_Registry should execute the specified tool and return a result.
**Validates: Requirements 5.6**

Property 8: Tool Result Return
*For any* tool execution that completes (success or failure), the Tool_Registry should return the result to the LLM_Provider for continued reasoning.
**Validates: Requirements 5.7**

Property 9: Workspace Path Validation
*For any* file operation request with a path parameter, the Tool_Registry should reject the operation if the path resolves to a location outside the configured Workspace directory.
**Validates: Requirements 5.9, 11.3, 11.4**

Property 10: Message Persistence
*For any* message (user message or agent response) processed by the system, the Memory_Store should persist that message to disk immediately after processing.
**Validates: Requirements 6.1, 6.2**

Property 11: Conversation History Round-Trip
*For any* conversation with messages saved to disk, restarting the system and loading the conversation should produce an equivalent conversation history with all messages preserved.
**Validates: Requirements 6.3, 6.5**

Property 12: Configuration Validation
*For any* invalid configuration (missing required fields, invalid values, malformed JSON), the Configuration_Manager should log descriptive errors and prevent system startup.
**Validates: Requirements 7.4**

Property 13: Skill Tool Registration
*For any* Skill loaded from the skills directory, all tools and message handlers defined by that Skill should be registered with the Tool_Registry and Message_Bus respectively.
**Validates: Requirements 8.2, 8.3**

Property 14: Skill Load Failure Isolation
*For any* Skill that fails to load (missing dependencies, invalid format, runtime error), the system should log the error and continue initialization with remaining Skills.
**Validates: Requirements 8.5**

Property 15: Scheduled Task Execution
*For any* scheduled task whose cron expression matches the current time, the Scheduler should execute the associated action by publishing the appropriate message to the Message_Bus.
**Validates: Requirements 9.2**

Property 16: Scheduled Task Failure Isolation
*For any* scheduled task that fails during execution, the system should log the error and continue processing subsequent scheduled tasks without interruption.
**Validates: Requirements 9.5**

Property 17: Subagent Creation
*For any* task identified as long-running (execution time > threshold), the system should create a background subagent to handle the task asynchronously.
**Validates: Requirements 10.1**

Property 18: Subagent Completion Notification
*For any* subagent that completes its task, the system should send a notification message to the user through the Chat_Platform from which the task was initiated.
**Validates: Requirements 10.2**

Property 19: Subagent State Persistence
*For any* running subagents when shutdown occurs, the system should persist subagent state to disk such that restarting the system allows resumption of those subagents.
**Validates: Requirements 10.4**

Property 20: Tool Parameter Validation
*For any* tool invocation with parameters, the Tool_Registry should validate parameter types and ranges against the tool's schema before execution.
**Validates: Requirements 11.1**

Property 21: Shell Command Privilege Restriction
*For any* shell command execution, the system should execute the command with normal user privileges unless elevated privileges are explicitly configured for that command.
**Validates: Requirements 11.6**

Property 22: Error Logging with Stack Traces
*For any* error or exception that occurs during system operation, the logging system should record the error message along with the complete stack trace.
**Validates: Requirements 12.1**

Property 23: Connection Event Logging
*For any* Chat_Platform connection event (connect, disconnect, reconnect), the system should log the event with timestamp, platform name, and event type.
**Validates: Requirements 12.2**

Property 24: LLM API Call Logging
*For any* LLM_Provider API call, the system should log the request (model, parameters) and response (tokens, completion) for observability.
**Validates: Requirements 12.3**

Property 25: Tool Execution Logging
*For any* tool execution, the system should log the tool name, parameters, execution time, and result (success or failure).
**Validates: Requirements 12.4**

## Error Handling

### Error Categories

**Configuration Errors:**
- Missing required configuration fields
- Invalid configuration values (e.g., negative timeouts)
- Malformed JSON in configuration files
- Missing API keys or credentials
- Invalid file paths

**Strategy:** Fail fast on startup with descriptive error messages. Use data annotations and IValidateOptions for validation.

**Runtime Errors:**
- LLM API failures (rate limits, network errors, invalid requests)
- Chat platform connection failures
- Tool execution failures (file not found, permission denied, timeout)
- Memory store I/O errors

**Strategy:** Log errors with full context, return error messages to users, implement retry logic with exponential backoff for transient failures.

**Security Errors:**
- Path traversal attempts in file operations
- Shell command injection attempts
- Unauthorized access attempts

**Strategy:** Reject operations immediately, log security events, do not expose internal details to users.

### Error Handling Patterns

**LLM Provider Errors:**
```csharp
try
{
    var response = await _llmProvider.CompleteAsync(request, ct);
    return response;
}
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
{
    _logger.LogWarning("Rate limit exceeded, retrying after delay");
    await Task.Delay(TimeSpan.FromSeconds(60), ct);
    return await _llmProvider.CompleteAsync(request, ct);
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "LLM API request failed");
    return new LlmResponse { Error = $"API request failed: {ex.Message}" };
}
```

**Tool Execution Errors:**
```csharp
try
{
    ValidateParameters(parameters);
    var result = await tool.ExecuteAsync(parameters, ct);
    return new ToolResult { Success = true, Result = result };
}
catch (SecurityException ex)
{
    _logger.LogWarning(ex, "Security violation in tool execution");
    return new ToolResult { Success = false, Result = "Operation not permitted" };
}
catch (Exception ex)
{
    _logger.LogError(ex, "Tool execution failed");
    return new ToolResult { Success = false, Result = $"Execution failed: {ex.Message}" };
}
```

**Chat Platform Errors:**
```csharp
protected override async Task ExecuteAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        try
        {
            await _platform.ConnectAsync(ct);
            await ProcessMessagesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat platform connection failed, retrying");
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }
}
```

## Testing Strategy

### Dual Testing Approach

The testing strategy employs both unit tests and property-based tests as complementary approaches:

**Unit Tests:**
- Verify specific examples and edge cases
- Test integration points between components
- Test error conditions with known inputs
- Test platform-specific behavior (Windows Service, systemd)
- Test configuration loading with specific config files

**Property-Based Tests:**
- Verify universal properties across all inputs
- Test with randomized inputs to find edge cases
- Validate correctness properties from the design
- Test invariants that should always hold
- Each property test runs minimum 100 iterations

### Property-Based Testing Configuration

**Library:** Use `FsCheck` for .NET property-based testing (mature, well-documented, integrates with xUnit/NUnit)

**Test Configuration:**
- Minimum 100 iterations per property test
- Each test tagged with: `Feature: dotnet-port, Property {N}: {property_text}`
- Custom generators for domain types (messages, tool calls, configurations)
- Shrinking enabled to find minimal failing cases

**Example Property Test:**
```csharp
[Property(MaxTest = 100)]
[Trait("Feature", "dotnet-port")]
[Trait("Property", "9: Workspace Path Validation")]
public Property WorkspacePathValidation_RejectsPathsOutsideWorkspace()
{
    return Prop.ForAll(
        Arb.From<string>(), // Generate random paths
        path =>
        {
            var workspace = "/home/user/.nanobot/workspace";
            var toolRegistry = new ToolRegistry(workspace);
            var fileReadTool = toolRegistry.GetTool("file_read");
            
            var parameters = JsonSerializer.SerializeToElement(new { path });
            var result = fileReadTool.ExecuteAsync(parameters, CancellationToken.None).Result;
            
            var resolvedPath = Path.GetFullPath(path);
            var isInWorkspace = resolvedPath.StartsWith(workspace);
            
            return isInWorkspace ? result.Success : !result.Success;
        });
}
```

### Test Organization

**Unit Tests:**
- `Nanobot.Tests.Unit/Configuration/` - Configuration loading and validation
- `Nanobot.Tests.Unit/MessageBus/` - Message routing and pub/sub
- `Nanobot.Tests.Unit/Tools/` - Individual tool implementations
- `Nanobot.Tests.Unit/LlmProviders/` - LLM provider clients
- `Nanobot.Tests.Unit/ChatPlatforms/` - Chat platform adapters
- `Nanobot.Tests.Unit/Memory/` - Memory store operations

**Property Tests:**
- `Nanobot.Tests.Properties/` - All property-based tests organized by requirement area
- Each correctness property has one corresponding property test
- Tests reference design document property numbers in attributes

**Integration Tests:**
- `Nanobot.Tests.Integration/` - End-to-end scenarios with real components
- Test service startup and shutdown
- Test message flow from chat platform through agent to response
- Test tool execution in realistic scenarios

### Testing Tools and Execution

**Unit Tests:** Focus on specific examples, edge cases, and error conditions. Avoid writing too many unit tests - property-based tests handle comprehensive input coverage.

**Property Tests:** Focus on universal properties that hold for all inputs. Use randomization to discover edge cases automatically.

**Coverage Goals:**
- All correctness properties implemented as property tests
- All error handling paths covered by unit tests
- All chat platform integrations tested with examples
- All LLM providers tested with examples
- All tools tested with examples and properties

### Continuous Integration

- Run all tests on every commit
- Property tests run with 100 iterations in CI
- Integration tests run against mock services
- Code coverage reporting (target: >80% for core logic)
- Static analysis with Roslyn analyzers
