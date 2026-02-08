#!/bin/bash
# Build Script for CoreBot
# This script builds CoreBot for production deployment

set -e

# Default values
CONFIGURATION="Release"
RUNTIME="both"
SELF_CONTAINED=true
SINGLE_FILE=true
TRIMMED=false

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        -r|--runtime)
            RUNTIME="$2"
            shift 2
            ;;
        --no-self-contained)
            SELF_CONTAINED=false
            shift
            ;;
        --no-single-file)
            SINGLE_FILE=false
            shift
            ;;
        --trimmed)
            TRIMMED=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  -c, --configuration CONFIG    Build configuration (Debug or Release, default: Release)"
            echo "  -r, --runtime RUNTIME          Target runtime (win-x64, linux-x64, or both, default: both)"
            echo "  --no-self-contained           Don't create self-contained build"
            echo "  --no-single-file              Don't publish as single file"
            echo "  --trimmed                     Enable assembly trimming"
            echo "  -h, --help                   Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use -h or --help for usage"
            exit 1
            ;;
    esac
done

# Function to build project
build_project() {
    local runtime=$1
    echo -e "${CYAN}Building for $runtime...${NC}"

    local output_dir="CoreBot.Host/bin/$CONFIGURATION/net10.0/$runtime/publish"
    local args=(
        publish
        -c "$CONFIGURATION"
        -r "$runtime"
        --output "$output_dir"
        -p:PublishSingleFile=$SINGLE_FILE
        -p:SelfContained=$SELF_CONTAINED
        -p:PublishTrimmed=$TRIMMED
    )

    dotnet "${args[@]}"

    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓ Build successful for $runtime${NC}"
        echo -e "  ${NC}  Output: $output_dir"
    else
        echo -e "${RED}✗ Build failed for $runtime${NC}"
        exit 1
    fi
}

# Function to copy deployment files
copy_deployment_files() {
    local runtime=$1
    echo -e "${CYAN}Copying deployment files...${NC}"

    local publish_dir="CoreBot.Host/bin/$CONFIGURATION/net10.0/$runtime/publish"

    # Copy config template
    if [ -f "deployment/config.json" ]; then
        cp "deployment/config.json" "$publish_dir/config-template.json"
        echo -e "${GREEN}✓ Config template copied${NC}"
    fi

    # Copy installation scripts
    if [ "$runtime" = "win-x64" ]; then
        if [ -f "deployment/install-windows-service.ps1" ]; then
            cp "deployment/install-windows-service.ps1" "$publish_dir"
            echo -e "${GREEN}✓ Windows installation script copied${NC}"
        fi
    elif [ "$runtime" = "linux-x64" ]; then
        if [ -f "deployment/install-systemd-service.sh" ]; then
            cp "deployment/install-systemd-service.sh" "$publish_dir"
            chmod +x "$publish_dir/install-systemd-service.sh"
            echo -e "${GREEN}✓ Linux installation script copied${NC}"
        fi
        if [ -f "deployment/corebot.service" ]; then
            cp "deployment/corebot.service" "$publish_dir"
            echo -e "${GREEN}✓ systemd unit file copied${NC}"
        fi
    fi

    # Copy README
    if [ -f "README.md" ]; then
        cp "README.md" "$publish_dir"
        echo -e "${GREEN}✓ README copied${NC}"
    fi
}

# Main build process
echo ""
echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}  CoreBot Build Script${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""
echo -e "${WHITE}Configuration: $CONFIGURATION${NC}"
echo -e "${WHITE}Self-Contained: $SELF_CONTAINED${NC}"
echo -e "${WHITE}Single-File: $SINGLE_FILE${NC}"
echo -e "${WHITE}Trimmed: $TRIMMED${NC}"
echo ""

# Clean previous builds
echo -e "${CYAN}Cleaning previous builds...${NC}"
rm -rf CoreBot.Host/bin

# Build for specified runtime(s)
if [ "$RUNTIME" = "both" ]; then
    build_project "win-x64"
    copy_deployment_files "win-x64"
    echo ""

    build_project "linux-x64"
    copy_deployment_files "linux-x64"
elif [ "$RUNTIME" = "win-x64" ] || [ "$RUNTIME" = "linux-x64" ]; then
    build_project "$RUNTIME"
    copy_deployment_files "$RUNTIME"
else
    echo -e "${RED}Invalid runtime: $RUNTIME${NC}"
    echo "Valid runtimes: win-x64, linux-x64, both"
    exit 1
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  Build Complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "${CYAN}Next steps:${NC}"
if [ "$RUNTIME" = "both" ] || [ "$RUNTIME" = "win-x64" ]; then
    echo -e "${WHITE}  Windows: Navigate to publish directory and run:${NC}"
    echo -e "    .\\install-windows-service.ps1"
fi
if [ "$RUNTIME" = "both" ] || [ "$RUNTIME" = "linux-x64" ]; then
    echo -e "${WHITE}  Linux: Navigate to publish directory and run:${NC}"
    echo -e "    sudo ./install-systemd-service.sh"
fi
echo ""
echo -e "${YELLOW}Remember to:${NC}"
echo -e "${WHITE}  1. Edit config.json with your API keys and tokens${NC}"
echo -e "${WHITE}  2. Create ~/.corebot directory for configuration${NC}"
echo -e "${WHITE}  3. Copy config.json to ~/.corebot/config.json${NC}"
echo ""
