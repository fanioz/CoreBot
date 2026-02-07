# CoreBot

A multi-platform AI assistant bot supporting Telegram, WhatsApp, and Feishu with extensible skills and tool execution capabilities.

## Features

- **Multiple LLM Providers**: Support for OpenRouter, Anthropic Claude, OpenAI, DeepSeek, Groq, and Google Gemini
- **Chat Platform Integration**: Native support for Telegram Bot API, WhatsApp Business API, and Feishu Open API
- **Tool Execution**: Built-in tools for file operations, shell commands, web fetching, and message sending
- **Extensible Skills**: Plugin architecture for adding custom tools and message handlers
- **Scheduled Tasks**: Cron-based task scheduling for automation
- **Subagent System**: Background task management with state persistence
- **Cross-Platform**: Runs as Windows Service or systemd daemon

## Requirements

- .NET 10.0 Runtime or SDK
- Windows Server 2016+ or Linux with systemd
- API keys for LLM providers
- Bot tokens for chat platforms (optional)

## Installation

### Windows

#### Option 1: Manual Installation

1. Build the project:
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained
   ```

2. Navigate to the publish directory:
   ```powershell
   cd CoreBot.Host\bin\Release\net10.0\win-x64\publish
   ```

3. Create configuration file:
   ```powershell
   mkdir ~/.nanobot
   copy config.json ~/.nanobot/config.json
   notepad ~/.nanobot/config.json
   ```

4. Install as Windows Service (run as Administrator):
   ```powershell
   .\install-windows-service.ps1
   ```

#### Option 2: Using PowerShell Script

The installation script handles everything:
```powershell
# Run as Administrator
.\deployment\install-windows-service.ps1

# To uninstall:
.\deployment\install-windows-service.ps1 -Uninstall
```

### Linux

#### Option 1: Manual Installation

1. Install .NET 10.0 Runtime:
   ```bash
   # Ubuntu/Debian
   wget https://dot.net/v1/dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --channel 10.0 --runtime aspnetcore

   # RHEL/CentOS
   sudo dnf install dotnet-runtime-10.0
   ```

2. Build the project:
   ```bash
   dotnet publish -c Release -r linux-x64 --self-contained
   cd CoreBot.Host/bin/Release/net10.0/linux-x64/publish
   ```

3. Create configuration:
   ```bash
   mkdir -p ~/.nanobot
   cp config.json ~/.nanobot/config.json
   nano ~/.nanobot/config.json
   ```

4. Install as systemd service:
   ```bash
   sudo ./install-systemd-service.sh
   ```

#### Option 2: Using Installation Script

The installation script handles everything:
```bash
# To install:
sudo ./deployment/install-systemd-service.sh install

# To uninstall:
sudo ./deployment/install-systemd-service.sh uninstall
```

## Configuration

Configuration is loaded from `~/.nanobot/config.json` (or `./data/config.json` for portable installations). See `deployment/config.json` for a complete example.

### LLM Provider Configuration

Set your preferred LLM provider and API key:

```json
{
  "CoreBot": {
    "Llm": {
      "Provider": "openrouter",
      "OpenRouter": {
        "ApiKey": "sk-or-v1-your-key-here",
        "Model": "anthropic/claude-3.5-sonnet"
      }
    }
  }
}
```

### Chat Platform Configuration

Enable and configure chat platforms:

```json
{
  "CoreBot": {
    "ChatPlatforms": {
      "telegram": {
        "Enabled": true,
        "BotToken": "123456789:ABCdefGHIjklMNOpqrsTUVwxyz"
      }
    }
  }
}
```

### Tool Configuration

Configure tool permissions and constraints:

```json
{
  "CoreBot": {
    "Tools": {
      "WorkspacePath": "~/.nanobot/workspace",
      "ShellEnabled": true,
      "ShellTimeout": 30,
      "FileOperationsEnabled": true
    }
  }
}
```

## Usage

### Starting Manually (Development)

```bash
dotnet run --project CoreBot.Host
```

### Windows Service Management

```powershell
# Start service
Start-Service CoreBot

# Stop service
Stop-Service CoreBot

# Restart service
Restart-Service CoreBot

# View status
Get-Service CoreBot

# View logs
Get-EventLog -LogName Application -Source CoreBot -Newest 50
```

### Linux Service Management

```bash
# Start service
sudo systemctl start corebot

# Stop service
sudo systemctl stop corebot

# Restart service
sudo systemctl restart corebot

# View status
sudo systemctl status corebot

# View logs
sudo journalctl -u corebot -f

# Enable on boot
sudo systemctl enable corebot
```

## Developing Skills

Skills are .NET assemblies loaded from `~/.nanobot/skills/`. Create a skill by implementing the `ISkill` interface:

```csharp
using CoreBot.Core.Skills;
using CoreBot.Core.Tools;

public class MySkill : ISkill
{
    public string Name => "my-skill";
    public string Description => "My custom skill";
    public string Version => "1.0.0";

    public IEnumerable<IToolDefinition> GetTools()
    {
        return new[] { new MyCustomTool() };
    }

    public IEnumerable<IMessageHandler> GetMessageHandlers()
    {
        return Enumerable.Empty<IMessageHandler>();
    }

    public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
```

Build your skill as a DLL and place it in `~/.nanobot/skills/`. CoreBot will automatically load it on startup.

## Building for Production

### Single-File Publishing

```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Linux
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true

# With trimming for smaller size
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
```

### Output Location

Published files are in: `CoreBot.Host/bin/Release/net10.0/<runtime>/publish/`

## Troubleshooting

### Service Won't Start

1. Check configuration is valid:
   ```bash
   # Windows
   type ~/.nanobot/config.json

   # Linux
   cat ~/.nanobot/config.json
   ```

2. Check logs:
   ```bash
   # Windows
   Get-EventLog -LogName Application -Newest 100 | Where-Object { $_.Source -eq "CoreBot" }

   # Linux
   sudo journalctl -u corebot -n 100 --no-pager
   ```

3. Verify API keys and tokens are correct

4. Check data directory permissions:
   ```bash
   # Windows
   icacls .\data

   # Linux
   ls -la ~/.nanobot
   ```

### Permission Errors

- Ensure the service account has read/write access to the data directory
- On Linux, the service runs as the `corebot` user
- On Windows, the service runs as `LOCAL SYSTEM` by default

### Platform-Specific Issues

**Windows**:
- Ensure Windows Service hosting is installed: `dotnet add package Microsoft.Extensions.Hosting.WindowsServices`
- Check Windows Firewall settings if webhooks don't work

**Linux**:
- Ensure systemd hosting is installed: `dotnet add package Microsoft.Extensions.Hosting.Systemd`
- Check SELinux policies if accessing files outside home directory
- Verify systemd journal is accessible: `journalctl --disk-usage`

## Development

### Running Tests

```bash
# Unit tests
dotnet test CoreBot.Tests.Unit

# Property tests
dotnet test CoreBot.Tests.Properties

# All tests
dotnet test
```

### Project Structure

```
CoreBot.Core/              # Core library
├── ChatPlatforms/         # Chat platform adapters
├── Configuration/         # Configuration classes
├── LLM/                   # LLM provider clients
├── Memory/                # Conversation persistence
├── Messaging/             # Message bus
├── Messages/              # Message types
├── Services/              # Hosted services
├── Skills/                # Plugin system
├── Subagents/             # Background tasks
└── Tools/                 # Tool registry and built-in tools

CoreBot.Host/              # Application entry point
├── Program.cs             # Service host setup
└── appsettings.json       # Default configuration

CoreBot.Tests.Unit/        # Unit tests
CoreBot.Tests.Properties/  # Property-based tests
```

## License

[Your License Here]

## Contributing

[Your Contributing Guidelines Here]

## Support

For issues and questions, please open an issue on GitHub.
