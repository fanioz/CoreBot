# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the **CoreBot .NET Port** project - an initiative to port the Nanobot personal AI assistant from Python to .NET (C#) while maintaining its ultra-lightweight philosophy (<6,000 lines of code). The project adds native support for Windows Service and systemd daemon deployment.

**Project Status:** Early planning phase. No implementation code exists yet. The repository currently contains only specification documents in `.kiro/specs/dotnet-port/`.

**Key Design Principle:** Ultra-lightweight implementation that prefers .NET BCL (Base Class Library) over third-party dependencies.

## Architecture

The system follows a layered architecture with event-driven messaging at its core:

### Service Host Layer
- **Windows Service Support:** Uses `Microsoft.Extensions.Hosting.WindowsServices`
- **systemd Daemon Support:** Uses `Microsoft.Extensions.Hosting.Systemd`
- **Generic Host:** Built on .NET Generic Host for cross-platform service lifetime management

### Application Layer
- **Message Bus:** Central event routing using `System.Threading.Channels` for async message passing. Message types: `UserMessage`, `AgentResponse`, `ToolCall`, `ToolResult`, `SystemEvent`.
- **Agent Service:** `IHostedService` that processes messages, orchestrates LLM calls and tool execution, manages conversation context and subagent lifecycle.
- **Scheduler Service:** `IHostedService` using `System.Threading.PeriodicTimer` and `Cronos` library for cron-style scheduling.

### Integration Layer
- **Chat Platform Adapters:** Interface `IChatPlatform` with implementations for Telegram, WhatsApp, and Feishu. Each adapter runs as `IHostedService`.
- **LLM Provider Clients:** Interface `ILlmProvider` with implementations for OpenRouter, Anthropic, OpenAI, DeepSeek, Groq, and Google Gemini. Uses `HttpClient` with typed clients pattern.
- **Tool Registry:** Dictionary-based registry implementing `IToolDefinition` with workspace sandboxing. Built-in tools: `FileReadTool`, `FileWriteTool`, `ShellTool`, `WebFetchTool`, `SendMessageTool`.

### Storage Layer
- **Memory Store:** File-based JSON storage at `~/.nanobot/memory/{platform}/{user_id}/` using `System.Text.Json`.
- **Configuration Store:** Uses `Microsoft.Extensions.Configuration` with JSON files and environment variable overrides (`${VAR_NAME}` syntax).

## Core Interfaces

```csharp
// Message Bus - Central event routing
public interface IMessageBus
{
    ValueTask PublishAsync<T>(T message, CancellationToken ct = default) where T : IMessage;
    IAsyncEnumerable<T> SubscribeAsync<T>(CancellationToken ct = default) where T : IMessage;
}

// Chat Platform - External messaging service abstraction
public interface IChatPlatform
{
    string PlatformName { get; }
    Task ConnectAsync(CancellationToken ct);
    Task SendMessageAsync(string userId, string message, CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
}

// LLM Provider - AI model abstraction
public interface ILlmProvider
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct);
    IAsyncEnumerable<LlmStreamChunk> StreamCompleteAsync(LlmRequest request, CancellationToken ct);
}

// Tool Definition - Function calling abstraction
public interface IToolDefinition
{
    string Name { get; }
    string Description { get; }
    JsonDocument GetSchema();
    Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct);
}

// Memory Store - Conversation persistence
public interface IMemoryStore
{
    Task SaveMessageAsync(ConversationMessage message, CancellationToken ct);
    Task<List<ConversationMessage>> GetHistoryAsync(string platform, string userId, int limit, CancellationToken ct);
}
```

## .NET Conventions

This codebase follows .NET-native patterns and conventions:

- **Async/Await:** All I/O operations use async/await
- **Dependency Injection:** Component composition via Microsoft.Extensions.DependencyInjection
- **Hosted Services:** Background services implement `IHostedService` or extend `BackgroundService`
- **IOptions Pattern:** Configuration uses `IOptions<T>` and `IOptionsSnapshot<T>`
- **JSON Serialization:** Uses `System.Text.Json` (not Newtonsoft.Json)
- **Naming Conventions:** PascalCase for public members, camelCase for private members, _camelCase for private fields
- **Nullable Reference Types:** Enabled throughout to prevent null reference exceptions
- **C# 12 Features:** Project targets C# 12+ for latest language features

## Implementation Approach

The implementation follows a **bottom-up strategy** as defined in `.kiro/specs/dotnet-port/tasks.md`:

1. **Core Infrastructure:** Project structure, DI container, hosting, logging
2. **Configuration System:** JSON file loading with environment variable overrides
3. **Message Bus:** Pub/sub implementation using System.Threading.Channels
4. **Memory Store:** JSON file persistence for conversations
5. **Tool Registry:** Built-in tools with workspace sandboxing
6. **LLM Providers:** HTTP clients for various AI providers
7. **Chat Platform Adapters:** Integrations with messaging services
8. **Agent Service:** Main orchestration logic
9. **Scheduler:** Cron-based task scheduling
10. **Subagents:** Background task execution
11. **Skills System:** Plugin architecture for extensibility
12. **Service Host:** Windows Service and systemd hosting

## Testing Strategy

The project uses a **dual testing approach**:

### Property-Based Tests
- Use **FsCheck** library for .NET
- Validate universal correctness properties from the design document
- Minimum 100 iterations per property test
- Tests tagged with: `Feature: dotnet-port, Property {N}: {property_text}`
- Property definitions are in `.kiro/specs/dotnet-port/design.md` (25 properties defined)

### Unit Tests
- Verify specific examples and edge cases
- Test integration points and error conditions
- Test platform-specific behavior (Windows Service, systemd)
- Keep unit tests minimal - property tests provide comprehensive input coverage

### Test Organization
```
CoreBot.Tests.Unit/          # Unit tests by component
CoreBot.Tests.Properties/    # Property-based tests
CoreBot.Tests.Integration/   # End-to-end scenarios
```

## Key Constraints

- **Line Count:** Must contain < 6,000 lines of C# code (excluding comments and blank lines)
- **Startup Time:** Must complete initialization within 2 seconds
- **Memory Usage:** Must consume < 100MB under normal operation
- **Dependencies:** Prefer .NET BCL over third-party libraries. Minimal external dependencies: `Microsoft.Extensions.Hosting.WindowsServices`, `Microsoft.Extensions.Hosting.Systemd`, `Cronos`, `FsCheck` (test only)
- **Deployment:** Support single-file deployment or minimal file count

## Safety and Sandboxing

Security is a core requirement:

- **Workspace Path Validation:** All file operations must verify paths are within the configured workspace directory
- **Shell Timeouts:** All shell commands enforce a 30-second timeout (configurable)
- **Parameter Validation:** Tool parameters are validated against JSON schemas before execution
- **Privilege Restriction:** Shell commands execute with normal user privileges unless explicitly configured
- **Path Traversal Prevention:** File operations reject paths outside workspace

## Configuration Management

Configuration is loaded from (in priority order):
1. `appsettings.json` (application defaults)
2. `~/.nanobot/config.json` (user configuration)
3. Environment variables (for sensitive values like API keys)

Environment variable overrides use `${VAR_NAME}` syntax in configuration files.

Example configuration:
```json
{
  "llm": {
    "provider": "openrouter",
    "apiKey": "${OPENROUTER_API_KEY}",
    "model": "anthropic/claude-3-5-sonnet"
  },
  "chatPlatforms": {
    "telegram": {
      "enabled": true,
      "apiKey": "${TELEGRAM_BOT_TOKEN}"
    }
  },
  "tools": {
    "workspacePath": "~/.nanobot/workspace",
    "shellTimeoutSeconds": 30
  }
}
```

## Development Workflow

When implementing features:

1. **Read the Task List:** Check `.kiro/specs/dotnet-port/tasks.md` for the current task and acceptance criteria
2. **Check Requirements:** Verify which requirements each task validates in `.kiro/specs/dotnet-port/requirements.md`
3. **Review Design:** Understand component interfaces and data models in `.kiro/specs/dotnet-port/design.md`
4. **Implement Code:** Write minimal, focused implementation following .NET conventions
5. **Write Property Tests:** Implement property tests for correctness properties (marked with `*` in task list)
6. **Write Unit Tests:** Add unit tests for edge cases and error handling
7. **Checkpoint:** Run all tests at designated checkpoints (tasks 5, 8, 13, 18)

## Property Testing

The design document defines 25 correctness properties that must be validated via property-based testing. Each property test:

- Runs minimum 100 iterations
- Uses FsCheck's `Prop.ForAll()` for universal quantification
- Tests a specific requirement from the requirements document
- Is tagged with the property number and description

Example property test structure:
```csharp
[Property(MaxTest = 100)]
[Trait("Feature", "dotnet-port")]
[Trait("Property", "9: Workspace Path Validation")]
public Property WorkspacePathValidation_RejectsPathsOutsideWorkspace()
{
    return Prop.ForAll(
        Arb.From<string>(),
        path => /* Test implementation */
    );
}
```

## Common Development Tasks

### Build the Solution
```bash
dotnet build CoreBot.sln
```

### Run All Tests
```bash
dotnet test CoreBot.sln
```

### Run Only Property Tests
```bash
dotnet test CoreBot.sln --filter "FullyQualifiedName~Properties"
```

### Run Single Test
```bash
dotnet test CoreBot.sln --filter "FullyQualifiedName~WorkspacePathValidation"
```

### Publish as Single File (Windows)
```bash
dotnet publish CoreBot.Host -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### Publish as Single File (Linux)
```bash
dotnet publish CoreBot.Host -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

### Run as Console Application (Development)
```bash
dotnet run --project CoreBot.Host
```

### Install as Windows Service
```powershell
sc.exe create CoreBot binPath="C:\Path\To\CoreBot.Host.exe" start=auto
sc.exe start CoreBot
```

### Install as systemd Daemon
```bash
sudo cp nanobot.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable nanobot
sudo systemctl start nanobot
```

## Code Style Guidelines

- **Minimal Code:** Write only what's needed. Avoid over-engineering.
- **No Premature Abstractions:** Don't create helpers for one-time operations.
- **Trust Internal Guarantees:** Don't add validation for conditions that can't happen.
- **No Feature Flags:** Make breaking changes directly; the codebase is not yet released.
- **Symbolic Editing:** Use symbol-level editing tools when modifying entire methods/classes.
- **Regex Editing:** Use regex-based editing for small changes within larger symbols.

## Error Handling Patterns

**Configuration Errors:** Fail fast on startup with descriptive error messages using data annotations and `IValidateOptions`.

**Runtime Errors:** Log with full context, return error messages to users, implement retry logic with exponential backoff for transient failures.

**Security Errors:** Reject immediately, log security events, don't expose internal details to users.

## Specification Documents

- **Requirements:** `.kiro/specs/dotnet-port/requirements.md` - 13 formal requirements with acceptance criteria
- **Design:** `.kiro/specs/dotnet-port/design.md` - Architecture, interfaces, data models, properties, error handling, testing strategy
- **Tasks:** `.kiro/specs/dotnet-port/tasks.md` - 18 major implementation tasks broken into 70+ subtasks

When working on this project, always reference these documents to ensure alignment with the specified requirements and design.
