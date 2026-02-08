# Common Commands for CoreBot Development

## Building and Running

### Build Solution
```powershell
dotnet build CoreBot.sln
```

### Run Application (Development)
```powershell
dotnet run --project CoreBot.Host
```

### Run with Specific Configuration
```powershell
dotnet run --project CoreBot.Host --configuration Release
```

## Testing

### Run All Tests
```powershell
dotnet test CoreBot.sln
```

### Run Only Unit Tests
```powershell
dotnet test CoreBot.Tests.Unit
```

### Run Only Property Tests
```powershell
dotnet test CoreBot.Tests.Properties
```

### Run Tests with Filter
```powershell
dotnet test CoreBot.sln --filter "FullyQualifiedName~WorkspacePathValidation"
```

### Run Tests in Specific File
```powershell
dotnet test --filter "FullyQualifiedName~ToolRegistryTests"
```

## Publishing

### Windows Single-File
```powershell
dotnet publish CoreBot.Host -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### Linux Single-File
```powershell
dotnet publish CoreBot.Host -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

### With Trimming (Smaller Size)
```powershell
dotnet publish CoreBot.Host -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
```

## Build Scripts

### Windows (PowerShell)
```powershell
.\build.ps1 -Runtime win-x64 -Configuration Release
```

### Linux (Bash)
```bash
./build.sh --runtime linux-x64 --configuration Release
```

## Installation

### Windows Service
```powershell
# Manual
sc.exe create CoreBot binPath="C:\Path\To\CoreBot.Host.exe" start=auto
sc.exe start CoreBot

# Using script
.\deployment\install-windows-service.ps1
```

### Linux systemd
```bash
# Manual
sudo cp deployment/corebot.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable corebot
sudo systemctl start corebot

# Using script
sudo ./deployment/install-systemd-service.sh install
```

## Service Management

### Windows
```powershell
Start-Service CoreBot
Stop-Service CoreBot
Restart-Service CoreBot
Get-Service CoreBot
Get-EventLog -LogName Application -Source CoreBot -Newest 50
```

### Linux
```bash
sudo systemctl start corebot
sudo systemctl stop corebot
sudo systemctl restart corebot
sudo systemctl status corebot
sudo journalctl -u corebot -f
```

## Configuration

### Create Config File
```powershell
# Windows
mkdir ~/.corebot
copy deployment/config.json ~/.corebot/config.json
notepad ~/.corebot/config.json
```

```bash
# Linux
mkdir -p ~/.corebot
cp deployment/config.json ~/.corebot/config.json
nano ~/.corebot/config.json
```

## Troubleshooting

### Check Configuration
```powershell
# Windows
type ~/.corebot/config.json

# Linux
cat ~/.corebot/config.json
```

### View Logs
```powershell
# Windows
Get-EventLog -LogName Application -Newest 100 | Where-Object { $_.Source -eq "CoreBot" }

# Linux
sudo journalctl -u corebot -n 100 --no-pager
```

### Check Files and Permissions
```powershell
# Windows
icacls .\data

# Linux
ls -la ~/.corebot
```

## Windows-Specific Utilities

### Find Files
```powershell
Get-ChildItem -Recurse -Filter "*.cs"
```

### Search Content
```powershell
Select-String -Path "*.cs" -Pattern "interface"
```

### Git Operations
```powershell
git status
git add .
git commit -m "message"
git push
```

## Line Count (Quality Check)

```bash
# Run the line count script
./line-count-report.sh
```

This verifies the project stays under 6,000 lines of C# code.
