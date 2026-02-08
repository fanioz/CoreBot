# CoreBot Project Overview

**Project Name:** CoreBot .NET Port
**Type:** Multi-platform AI Assistant Bot
**Language:** C# (.NET 10.0)
**Platform:** Windows (Windows Service) and Linux (systemd daemon)

## Purpose

CoreBot is a .NET port of the Python Nanobot personal AI assistant. It's an ultra-lightweight (<6,000 lines of code) multi-platform AI assistant that:
- Integrates with chat platforms (Telegram, WhatsApp, Feishu)
- Supports multiple LLM providers (OpenRouter, Anthropic, OpenAI, DeepSeek, Groq, Gemini)
- Executes tools (file operations, shell commands, web fetching)
- Runs scheduled tasks via cron
- Supports extensible plugins (skills)
- Manages background subagents with state persistence

## Tech Stack

- **Runtime:** .NET 10.0 with C# 12
- **Hosting:** Microsoft.Extensions.Hosting (Generic Host)
- **Service Mode:** Windows Service (UseWindowsService) or systemd (UseSystemd)
- **Configuration:** Microsoft.Extensions.Configuration with JSON + environment variables
- **DI:** Microsoft.Extensions.DependencyInjection
- **Serialization:** System.Text.Json
- **Async:** System.Threading.Channels for message bus
- **Testing:** xUnit + FsCheck (property-based testing)

## Project Structure

```
CoreBot.Core/              # Core library with all business logic
├── ChatPlatforms/         # Platform adapters (Telegram, WhatsApp, Feishu)
├── Configuration/         # Config classes and helpers
├── LLM/                   # LLM provider clients
├── Memory/                # Conversation persistence
├── Messaging/             # Message bus (pub/sub)
├── Messages/              # Message types
├── Services/              # Hosted services (Agent, Scheduler, etc.)
├── Skills/                # Plugin system
├── Subagents/             # Background task management
└── Tools/                 # Tool registry and built-in tools

CoreBot.Host/              # Application entry point
├── Program.cs             # DI container and service registration
└── appsettings.json       # Default configuration

CoreBot.Tests.Unit/        # Unit tests
CoreBot.Tests.Properties/  # Property-based tests (FsCheck)
deployment/                # Installation scripts and configs
```

## Key Architecture Patterns

1. **Message Bus:** Async pub/sub using System.Threading.Channels
2. **Hosted Services:** All background components implement IHostedService
3. **Options Pattern:** Configuration via IOptions<T>
4. **File-Based Storage:** JSON files for memory and state
5. **Plugin Architecture:** Skills loaded dynamically from DLL files

## Recent Updates

- **Renamed all paths** from `.nanobot` to `.corebot` for consistency
- Configuration directory: `~/.corebot/`
- Workspace: `~/.corebot/workspace`
- Skills: `~/.corebot/skills`
- Memory: `~/.corebot/memory/`
