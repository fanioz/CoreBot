# Quick Deployment Guide

This guide will help you deploy CoreBot in production on Windows or Linux.

## Prerequisites

### Windows
- Windows Server 2016+ or Windows 10+
- [.NET 10.0 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (or SDK for development)
- Administrator privileges

### Linux
- Linux distribution with systemd (Ubuntu 20.04+, RHEL 8+, Debian 11+, etc.)
- [.NET 10.0 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Root or sudo access

## Step 1: Build the Application

### Windows (PowerShell)
```powershell
# Run the build script
.\build.ps1

# Or build manually
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### Linux (Bash)
```bash
# Make script executable (first time only)
chmod +x build.sh

# Run the build script
./build.sh

# Or build manually
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

Output will be in: `CoreBot.Host/bin/Release/net10.0/<runtime>/publish/`

## Step 2: Configure

1. Navigate to the publish directory
2. Edit the configuration file:

### Windows
```powershell
cd CoreBot.Host\bin\Release\net10.0\win-x64\publish
mkdir ~/.corebot
copy config-template.json ~/.corebot/config.json
notepad ~/.corebot/config.json
```

### Linux
```bash
cd CoreBot.Host/bin/Release/net10.0/linux-x64/publish
mkdir -p ~/.corebot
cp config-template.json ~/.corebot/config.json
nano ~/.corebot/config.json
```

3. **Required Configuration Changes:**
   - Set your LLM provider API key
   - Enable and configure chat platforms (optional)
   - Adjust tool permissions as needed

```json
{
  "CoreBot": {
    "Llm": {
      "Provider": "openrouter",
      "OpenRouter": {
        "ApiKey": "sk-or-v1-YOUR_KEY_HERE"
      }
    }
  }
}
```

## Step 3: Install as Service

### Windows (as Administrator)
```powershell
# Run the installation script
.\install-windows-service.ps1

# Verify service is running
Get-Service CoreBot

# View logs
Get-EventLog -LogName Application -Source CoreBot -Newest 50
```

### Linux (with sudo)
```bash
# Run the installation script
sudo ./install-systemd-service.sh

# Verify service is running
sudo systemctl status corebot

# View logs
sudo journalctl -u corebot -f
```

## Step 4: Verify Installation

### Windows
```powershell
# Check service status
Get-Service CoreBot | Select-Object Name, Status, StartType

# View recent logs
Get-EventLog -LogName Application -Source CoreBot -Newest 20 |
    Format-Table TimeGenerated, EntryType, Message -AutoSize
```

### Linux
```bash
# Check service status
sudo systemctl status corebot

# View recent logs
sudo journalctl -u corebot -n 20 --no-pager

# Follow logs in real-time
sudo journalctl -u corebot -f
```

## Managing the Service

### Windows Commands
```powershell
# Start service
Start-Service CoreBot

# Stop service
Stop-Service CoreBot

# Restart service
Restart-Service CoreBot

# Uninstall service
.\install-windows-service.ps1 -Uninstall
```

### Linux Commands
```bash
# Start service
sudo systemctl start corebot

# Stop service
sudo systemctl stop corebot

# Restart service
sudo systemctl restart corebot

# Enable on boot
sudo systemctl enable corebot

# Disable on boot
sudo systemctl disable corebot

# Uninstall service
sudo ./install-systemd-service.sh uninstall
```

## Testing the Installation

Before installing as a service, test the application manually:

### Windows
```powershell
# Run interactively
.\CoreBot.Host.exe

# Press Ctrl+C to stop
```

### Linux
```bash
# Run interactively
./CoreBot.Host

# Press Ctrl+C to stop
```

## Troubleshooting

### Service Won't Start

1. **Check Configuration**
   - Verify config.json exists at `~/.corebot/config.json`
   - Validate JSON syntax: `cat ~/.corebot/config.json | python -m json.tool` (Linux)
   - Ensure API keys are set correctly

2. **Check Logs**
   - Windows: Event Viewer → Windows Logs → Application
   - Linux: `sudo journalctl -u corebot -n 100`

3. **Check Permissions**
   - Windows: Ensure service account has access to data directory
   - Linux: Ensure `corebot` user owns `/var/lib/corebot`

4. **Verify Dependencies**
   - Ensure .NET 10.0 Runtime is installed
   - Check all required DLLs are in the publish directory

### Common Issues

**Issue**: Service starts but stops immediately
- **Solution**: Check configuration file for errors
- **Windows**: `type ~/.corebot/config.json`
- **Linux**: `cat ~/.corebot/config.json`

**Issue**: LLM API calls fail
- **Solution**: Verify API key is correct and has sufficient credits
- **Test**: Try making a manual API call with curl

**Issue**: Chat platform webhooks not received
- **Solution**: Check firewall settings and ensure port is open
- **Test**: Use ngrok or similar for testing: `ngrok http 8080`

**Issue**: File operations fail
- **Solution**: Check workspace path permissions
- **Configuration**: Set `"WorkspacePath"` to a writable directory

## Updating the Service

### Windows
```powershell
# 1. Stop service
Stop-Service CoreBot

# 2. Replace executable and files
# Copy new files to publish directory

# 3. Start service
Start-Service CoreBot
```

### Linux
```bash
# 1. Stop service
sudo systemctl stop corebot

# 2. Replace executable and files
sudo cp CoreBot.Host /usr/bin/
sudo chmod +x /usr/bin/CoreBot.Host

# 3. Start service
sudo systemctl start corebot
```

## Uninstallation

### Windows
```powershell
# Stop and remove service
.\install-windows-service.ps1 -Uninstall

# Remove files (optional)
Remove-Item -Recurse -Force ~\.corebot
Remove-Item -Recurse -Force .\data
```

### Linux
```bash
# Stop and remove service
sudo ./install-systemd-service.sh uninstall

# Remove user and data (optional)
sudo userdel corebot
sudo rm -rf /var/lib/corebot
rm -rf ~/.corebot
```

## Getting Help

- **Documentation**: See [README.md](README.md) for full documentation
- **Issues**: Report bugs on GitHub Issues
- **Logs**: Always include logs from the Event Viewer (Windows) or journalctl (Linux)
- **Configuration**: Include your `config.json` with sensitive values removed

## Security Considerations

1. **API Keys**: Never commit API keys to version control
2. **File Permissions**: Ensure config.json has appropriate permissions (600 on Linux)
3. **Service Account**: Run with minimal required privileges
4. **Network**: Use HTTPS webhooks when possible
5. **Updates**: Keep .NET runtime and dependencies updated

## Performance Tuning

### Memory Usage
- Limit conversation history size in configuration
- Disable unused chat platforms
- Reduce LLM max tokens if needed

### CPU Usage
- Increase `ShellTimeout` for long-running commands
- Adjust LLM `Temperature` for faster responses
- Disable unused tools

### Disk Usage
- Configure log rotation in `Logging.File.MaxFiles`
- Clean up old conversation history periodically
- Adjust `DataDirectory` path as needed

## Next Steps

1. Configure your preferred LLM provider
2. Set up chat platform webhooks (Telegram, WhatsApp, or Feishu)
3. Create custom skills for your use case
4. Configure scheduled tasks for automation
5. Monitor logs and adjust configuration as needed

For more details, see the full [README.md](README.md).
