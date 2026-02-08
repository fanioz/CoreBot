# Build Script for CoreBot
# This script builds CoreBot for production deployment

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [ValidateSet("win-x64", "linux-x64", "both")]
    [string]$Runtime = "both",

    [Parameter(Mandatory=$false)]
    [switch]$SelfContained = $true,

    [Parameter(Mandatory=$false)]
    [switch]$SingleFile = $true,

    [Parameter(Mandatory=$false)]
    [switch]$Trimmed = $false
)

$ErrorActionPreference = "Stop"

function Build-Project {
    param(
        [string]$Runtime,
        [string]$Configuration,
        [bool]$SelfContained,
        [bool]$SingleFile,
        [bool]$Trimmed
    )

    Write-Host "Building for $Runtime..." -ForegroundColor Cyan

    $outputDir = "CoreBot.Host\bin\$Configuration\net10.0\$Runtime\publish"
    $args = @(
        "publish"
        "-c", $Configuration
        "-r", $Runtime
        "--output", $outputDir
        "-p:PublishSingleFile=$SingleFile"
        "-p:SelfContained=$SelfContained"
        "-p:PublishTrimmed=$Trimmed"
    )

    & dotnet @args

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Build successful for $Runtime" -ForegroundColor Green
        Write-Host "  Output: $outputDir" -ForegroundColor Gray
    } else {
        Write-Host "✗ Build failed for $Runtime" -ForegroundColor Red
        exit 1
    }
}

function Copy-DeploymentFiles {
    param([string]$Runtime)

    Write-Host "Copying deployment files..." -ForegroundColor Cyan

    $publishDir = "CoreBot.Host\bin\$Configuration\net10.0\$Runtime\publish"

    # Copy config template
    if (Test-Path "deployment/config.json") {
        Copy-Item "deployment/config.json" "$publishDir/config-template.json" -Force
        Write-Host "✓ Config template copied" -ForegroundColor Green
    }

    # Copy installation scripts
    if ($Runtime -eq "win-x64") {
        if (Test-Path "deployment/install-windows-service.ps1") {
            Copy-Item "deployment/install-windows-service.ps1" $publishDir -Force
            Write-Host "✓ Windows installation script copied" -ForegroundColor Green
        }
    } elseif ($Runtime -eq "linux-x64") {
        if (Test-Path "deployment/install-systemd-service.sh") {
            Copy-Item "deployment/install-systemd-service.sh" $publishDir -Force
            & chmod +x "$publishDir/install-systemd-service.sh"
            Write-Host "✓ Linux installation script copied" -ForegroundColor Green
        }
        if (Test-Path "deployment/corebot.service") {
            Copy-Item "deployment/corebot.service" $publishDir -Force
            Write-Host "✓ systemd unit file copied" -ForegroundColor Green
        }
    }

    # Copy README
    if (Test-Path "README.md") {
        Copy-Item "README.md" $publishDir -Force
        Write-Host "✓ README copied" -ForegroundColor Green
    }
}

# Main build process
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  CoreBot Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Self-Contained: $SelfContained" -ForegroundColor White
Write-Host "Single-File: $SingleFile" -ForegroundColor White
Write-Host "Trimmed: $Trimmed" -ForegroundColor White
Write-Host ""

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Cyan
Remove-Item -Path "CoreBot.Host\bin" -Recurse -Force -ErrorAction SilentlyContinue

# Build for specified runtime(s)
if ($Runtime -eq "both") {
    Build-Project -Runtime "win-x64" -Configuration $Configuration -SelfContained $SelfContained -SingleFile $SingleFile -Trimmed $Trimmed
    Copy-DeploymentFiles -Runtime "win-x64"
    Write-Host ""

    Build-Project -Runtime "linux-x64" -Configuration $Configuration -SelfContained $SelfContained -SingleFile $SingleFile -Trimmed $Trimmed
    Copy-DeploymentFiles -Runtime "linux-x64"
} else {
    Build-Project -Runtime $Runtime -Configuration $Configuration -SelfContained $SelfContained -SingleFile $SingleFile -Trimmed $Trimmed
    Copy-DeploymentFiles -Runtime $Runtime
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
if ($Runtime -eq "both" -or $Runtime -eq "win-x64") {
    Write-Host "  Windows: Navigate to publish directory and run:" -ForegroundColor White
    Write-Host "    .\install-windows-service.ps1" -ForegroundColor Gray
}
if ($Runtime -eq "both" -or $Runtime -eq "linux-x64") {
    Write-Host "  Linux: Navigate to publish directory and run:" -ForegroundColor White
    Write-Host "    sudo ./install-systemd-service.sh" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Remember to:" -ForegroundColor Yellow
Write-Host "  1. Edit config.json with your API keys and tokens" -ForegroundColor White
Write-Host "  2. Create ~/.corebot directory for configuration" -ForegroundColor White
Write-Host "  3. Copy config.json to ~/.corebot/config.json" -ForegroundColor White
Write-Host ""
