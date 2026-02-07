#!/bin/bash
# Systemd Service Installation Script for CoreBot
# This script installs CoreBot as a systemd service on Linux

set -e

# Configuration
SERVICE_NAME="corebot"
SERVICE_FILE="corebot.service"
INSTALL_DIR="/usr/bin"
DATA_DIR="/var/lib/corebot"
LOG_DIR="/var/log/corebot"
CONFIG_DIR="/etc/corebot"
USER="corebot"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}Error: This script must be run as root (use sudo)${NC}"
    exit 1
fi

# Function to print colored messages
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Function to uninstall the service
uninstall_service() {
    print_info "Uninstalling $SERVICE_NAME service..."

    # Stop and disable the service
    systemctl stop $SERVICE_NAME 2>/dev/null || true
    systemctl disable $SERVICE_NAME 2>/dev/null || true

    # Remove service file
    rm -f /etc/systemd/system/$SERVICE_NAME
    systemctl daemon-reload

    # Ask if user wants to remove data directories
    read -p "Remove data directory $DATA_DIR? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        rm -rf $DATA_DIR
        print_info "Data directory removed"
    fi

    # Ask if user wants to remove user
    read -p "Remove user $USER? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        userdel $USER 2>/dev/null || print_warning "User $USER not found"
        print_info "User removed"
    fi

    print_info "Service $SERVICE_NAME uninstalled successfully"
}

# Function to create dedicated user
create_user() {
    if id "$USER" &>/dev/null; then
        print_info "User $USER already exists"
    else
        print_info "Creating user $USER..."
        useradd -r -s /bin/false -d $DATA_DIR $USER
        print_info "User $USER created"
    fi
}

# Function to install the service
install_service() {
    print_info "Installing $SERVICE_NAME service..."

    # Check if executable exists
    if [ ! -f "CoreBot.Host" ]; then
        print_error "Executable CoreBot.Host not found in current directory"
        print_info "Please build the project first: dotnet publish -c Release -r linux-x64 --self-contained"
        exit 1
    fi

    # Create user
    create_user

    # Create directories
    print_info "Creating directories..."
    mkdir -p $DATA_DIR
    mkdir -p $LOG_DIR
    mkdir -p $CONFIG_DIR

    # Copy executable
    print_info "Installing executable..."
    cp CoreBot.Host $INSTALL_DIR/
    chmod +x $INSTALL_DIR/CoreBot.Host

    # Copy service file
    print_info "Installing systemd service file..."
    cp $SERVICE_FILE /etc/systemd/system/

    # Set permissions
    print_info "Setting permissions..."
    chown -R $USER:$USER $DATA_DIR
    chown -R $USER:$USER $LOG_DIR
    chown -R $USER:$USER $CONFIG_DIR
    chmod 755 $DATA_DIR
    chmod 755 $LOG_DIR
    chmod 755 $CONFIG_DIR

    # Create symlink to config in data directory
    if [ ! -f "$DATA_DIR/config.json" ] && [ -f "$CONFIG_DIR/config.json" ]; then
        ln -s $CONFIG_DIR/config.json $DATA_DIR/config.json
    fi

    # Reload systemd
    print_info "Reloading systemd daemon..."
    systemctl daemon-reload

    # Enable service
    print_info "Enabling service..."
    systemctl enable $SERVICE_NAME

    # Start service
    print_info "Starting service..."
    systemctl start $SERVICE_NAME

    # Check service status
    sleep 2
    if systemctl is-active --quiet $SERVICE_NAME; then
        print_info "Service $SERVICE_NAME started successfully"
    else
        print_error "Service failed to start. Check logs with: journalctl -u $SERVICE_NAME -f"
        exit 1
    fi

    print_info "Installation complete!"
    print_info "Service management commands:"
    echo "  Start:   sudo systemctl start $SERVICE_NAME"
    echo "  Stop:    sudo systemctl stop $SERVICE_NAME"
    echo "  Restart: sudo systemctl restart $SERVICE_NAME"
    echo "  Status:  sudo systemctl status $SERVICE_NAME"
    echo "  Logs:    sudo journalctl -u $SERVICE_NAME -f"
}

# Parse command line arguments
case "${1:-install}" in
    uninstall)
        uninstall_service
        ;;
    install)
        install_service
        ;;
    *)
        echo "Usage: $0 [install|uninstall]"
        echo "  install   - Install CoreBot as a systemd service (default)"
        echo "  uninstall - Uninstall CoreBot systemd service"
        exit 1
        ;;
esac
