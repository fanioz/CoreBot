# Windows Service Installation Script for CoreBot
# This script installs CoreBot as a Windows Service

param(
    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "CoreBot",

    [Parameter(Mandatory=$false)]
    [string]$DisplayName = "CoreBot AI Assistant",

    [Parameter(Mandatory=$false)]
    [string]$Description = "CoreBot - Multi-platform AI assistant with support for Telegram, WhatsApp, and Feishu",

    [Parameter(Mandatory=$false)]
    [string]$ExecutablePath = "CoreBot.Host.exe",

    [Parameter(Mandatory=$false)]
    [string]$Arguments = "",

    [Parameter(Mandatory=$false)]
    [switch]$Uninstall
)

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be run as Administrator. Please right-click and select 'Run as Administrator'."
    exit 1
}

# Function to uninstall the service
function Uninstall-Service {
    Write-Host "Uninstalling $ServiceName service..."

    # Check if service exists
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

    if ($service) {
        # Stop the service if running
        if ($service.Status -eq "Running") {
            Write-Host "Stopping service..."
            Stop-Service -Name $ServiceName -Force
            Start-Sleep -Seconds 2
        }

        # Remove the service
        Write-Host "Removing service..."
        & sc.exe delete $ServiceName
        Write-Host "Service $ServiceName uninstalled successfully." -ForegroundColor Green
    } else {
        Write-Host "Service $ServiceName does not exist." -ForegroundColor Yellow
    }
}

# Function to install the service
function Install-Service {
    Write-Host "Installing $ServiceName service..."

    # Check if service already exists
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Error "Service $ServiceName already exists. Please uninstall it first using -Uninstall switch."
        exit 1
    }

    # Check if executable exists
    if (-not (Test-Path $ExecutablePath)) {
        Write-Error "Executable not found at: $ExecutablePath"
        Write-Host "Please make sure you've built the project and the executable exists."
        exit 1
    }

    # Get absolute path
    $absolutePath = (Resolve-Path $ExecutablePath).Path
    $binPath = "`"$absolutePath`""

    if (-not [string]::IsNullOrEmpty($Arguments)) {
        $binPath += " $Arguments"
    }

    # Create the service
    Write-Host "Creating service..."
    & sc.exe create $ServiceName binPath= $binPath DisplayName= "$DisplayName" start= auto

    # Set service description
    & sc.exe description $ServiceName "$Description"

    # Configure service to restart on failure
    & sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/120000/restart/300000

    Write-Host "Service $ServiceName installed successfully." -ForegroundColor Green

    # Start the service
    Write-Host "Starting service..."
    Start-Service -Name $ServiceName

    $service = Get-Service -Name $ServiceName
    if ($service.Status -eq "Running") {
        Write-Host "Service $ServiceName started successfully." -ForegroundColor Green
    } else {
        Write-Warning "Service installed but may not be running. Please check the Windows Event Logs."
    }
}

# Main script logic
if ($Uninstall) {
    Uninstall-Service
} else {
    Install-Service
}

# Display service status
Write-Host "`nService Status:"
Get-Service -Name $ServiceName -ErrorAction SilentlyContinue | Format-Table Name, Status, DisplayName, StartType -AutoSize

Write-Host "`nTo view logs, open Windows Event Viewer and navigate to:"
Write-Host "  -> Windows Logs -> Application"
Write-Host "  -> Filter by Source: CoreBot"
