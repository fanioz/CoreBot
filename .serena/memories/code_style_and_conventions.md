# CoreBot Code Style and Conventions

## Naming Conventions

- **Public members:** PascalCase (`public string UserName`)
- **Private members:** camelCase (`private string userId`)
- **Private fields:** _camelCase (`private readonly IMessageBus _messageBus`)
- **Interfaces:** IPascalCase (`public interface IMessageBus`)
- **Classes:** PascalCase (`public class MessageBus`)
- **Methods:** PascalCase (`public async Task SendMessageAsync()`)
- **Constants:** PascalCase (`public const int MaxRetries`)
- **Local variables:** camelCase (`var userId =`)

## Code Style Principles

1. **Minimal Code:** Write only what's needed. Avoid over-engineering.
2. **No Premature Abstractions:** Don't create helpers for one-time operations.
3. **Trust Internal Guarantees:** Don't add validation for conditions that can't happen.
4. **No Feature Flags:** Make breaking changes directly; not yet released.
5. **Async/Await:** All I/O operations MUST use async/await
6. **Dependency Injection:** All dependencies via constructor injection

## C# Language Features

- **C# 12+** features encouraged
- **Nullable Reference Types:** Enabled - make use of `?` and `!` appropriately
- **Pattern Matching:** Use switch expressions when appropriate
- **Records:** Use for immutable data types
- **File-scoped namespaces:** Preferred over traditional braces

## Async Patterns

```csharp
// CORRECT - All I/O is async
public async Task<string> ReadFileAsync(string path, CancellationToken ct = default)
{
    return await File.ReadAllTextAsync(path, ct);
}

// WRONG - Blocking async code
public string ReadFile(string path)
{
    return File.ReadAllText(path);  // Don't do this
}
```

## Configuration Pattern

```csharp
// Use IOptions<T> for configuration
public class MyService
{
    private readonly MyConfiguration _config;
    
    public MyService(IOptions<MyConfiguration> options)
    {
        _config = options.Value;
    }
}
```

## Hosted Services

```csharp
// Extend BackgroundService for long-running services
public class MyService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Do work
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
```

## Error Handling

- **Configuration Errors:** Fail fast on startup with descriptive messages
- **Runtime Errors:** Log with full context, return user-friendly messages
- **Security Errors:** Reject immediately, don't expose internal details

```csharp
// Configuration validation
services.AddOptions<MyConfiguration>()
    .Bind(config.GetSection("MySection"))
    .Validate(config => !string.IsNullOrEmpty(config.ApiKey), "API key is required")
    .ValidateOnStart();

// Runtime error handling
try
{
    await operation.ExecuteAsync(ct);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Operation failed: {Message}", ex.Message);
    throw new OperationFailedException("Operation failed. Please try again.", ex);
}
```

## Comments and Documentation

```csharp
/// <summary>
/// Public interface documentation required
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message to all subscribers
    /// </summary>
    /// <typeparam name="T">Message type implementing IMessage</typeparam>
    /// <param name="message">The message to publish</param>
    /// <param name="ct">Cancellation token</param>
    ValueTask PublishAsync<T>(T message, CancellationToken ct = default) 
        where T : IMessage;
}

// Private methods rarely need comments
private void ValidatePath(string path)
{
    // Code should be self-explanatory
    if (string.IsNullOrEmpty(path)) throw new ArgumentException(nameof(path));
}
```

## Testing Style

### Unit Tests
```csharp
public class MessageBusTests
{
    [Fact]
    public async Task PublishAsync_WhenMessagePublished_SubscriberReceivesIt()
    {
        // Arrange
        var bus = new MessageBus();
        
        // Act
        await bus.PublishAsync(new TestMessage());
        
        // Assert
        Assert.True(messageReceived);
    }
}
```

### Property Tests
```csharp
[Property(MaxTest = 100)]
[Trait("Feature", "dotnet-port")]
[Trait("Property", "1: Message Delivery")]
public Property MessageDelivery_AllPublishedMessagesReachSubscribers()
{
    return Prop.ForAll(
        Arb.From<int>(),
        messageId => /* Property test implementation */
    );
}
```

## File Organization

- **One class per file** (unless tiny related types)
- **File name matches class name** (`MessageBus.cs` contains `class MessageBus`)
- **Namespace matches directory structure** (`CoreBot.Core.Messaging` for `CoreBot/Core/Messaging/`)
- **Using statements:** System.* first, then Microsoft.*, then project namespaces

## Security Guidelines

1. **Workspace Validation:** All file operations must validate paths are within workspace
2. **Shell Timeouts:** All shell commands must have timeout (default 30 seconds)
3. **Parameter Validation:** Tool parameters validated against JSON schemas
4. **No Secrets in Code:** Use environment variables with `${VAR_NAME}` syntax
5. **Path Traversal Prevention:** Reject paths containing `..` outside workspace

## Quality Constraints

- **< 6,000 lines** of C# code (excluding comments/blank lines)
- **< 2 seconds** startup time
- **< 100MB** memory usage under normal operation
- **Single-file deployment** supported
- **Minimal dependencies** - prefer .NET BCL

## When Completing Tasks

1. Run all tests: `dotnet test CoreBot.sln`
2. Check line count: `./line-count-report.sh`
3. Verify build succeeds: `dotnet build CoreBot.sln`
4. Ensure no warnings (especially nullable warnings)
5. Test on both Windows and Linux if platform-specific
