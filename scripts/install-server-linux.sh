#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# USBAnywhere Server for Linux & Raspberry Pi - 1-Click Installer
# Copyright (c) 2025-2026 oscarlosu-lab - https://github.com/oscarlosu-lab/USBAnywhere-Server
# Licensed under GPL v3
# ─────────────────────────────────────────────────────────────────────────────

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${CYAN}╔══════════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║     🌐 USBAnywhere Server for Linux / Raspberry Pi Installer         ║${NC}"
echo -e "${CYAN}║            Universal Control Suite & 1-Click Auto-Setup              ║${NC}"
echo -e "${CYAN}╚══════════════════════════════════════════════════════════════════════╝${NC}"

if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}[✗] Please run as root (use sudo).${NC}"
    exit 1
fi

echo -e "${BLUE}[1/5] Detecting package manager & installing dependencies...${NC}"
if command -v apt-get &> /dev/null; then
    apt-get update -qq
    apt-get install -y -qq usbip hwdata linux-tools-generic linux-tools-$(uname -r) python3 2>/dev/null || apt-get install -y -qq usbip hwdata python3
elif command -v dnf &> /dev/null; then
    dnf install -y -q usbip python3
elif command -v pacman &> /dev/null; then
    pacman -Sy --noconfirm usbip python
else
    echo -e "${YELLOW}[!] Unknown package manager. Please ensure 'usbip' and 'python3' are installed.${NC}"
fi

echo -e "${BLUE}[2/5] Loading USB/IP kernel modules...${NC}"
modprobe usbip_core 2>/dev/null || true
modprobe usbip_host 2>/dev/null || true

# Persist modules across reboot
cat << 'EOF' > /etc/modules-load.d/usbip.conf
usbip_core
usbip_host
EOF

echo -e "${BLUE}[3/5] Installing systemd background service (usbipd.service)...${NC}"
cat << 'EOF' > /etc/systemd/system/usbipd.service
[Unit]
Description=USBAnywhere USB/IP Server Daemon
After=network.target network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/usr/sbin/usbipd -4
Restart=always
RestartSec=3
KillMode=process

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable usbipd
systemctl restart usbipd

echo -e "${BLUE}[4/5] Configuring udev auto-bind rule for USB peripherals...${NC}"
cat << 'EOF' > /etc/udev/rules.d/99-usbip-autobind.rules
# USBAnywhere udev auto-bind rule
ACTION=="add", SUBSYSTEM=="usb", ENV{DEVTYPE}=="usb_device", RUN+="/usr/bin/usbip bind -b %k"
EOF

udevadm control --reload-rules || true

echo -e "${BLUE}[5/5] Installing unified Control Suite (usbanywhere-ctl)...${NC}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -f "$SCRIPT_DIR/usbanywhere-ctl" ]; then
    cp "$SCRIPT_DIR/usbanywhere-ctl" /usr/local/bin/usbanywhere-ctl
    chmod +x /usr/local/bin/usbanywhere-ctl
else
    # Download latest if running via curl pipe
    curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere-Server/main/scripts/usbanywhere-ctl -o /usr/local/bin/usbanywhere-ctl
    chmod +x /usr/local/bin/usbanywhere-ctl
fi

echo -e "\n${GREEN}╔══════════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║  ✓ USBAnywhere Server & Control Suite installed successfully!        ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════════════════════╝${NC}"
echo -e "• Server daemon is listening on TCP port ${BOLD}3240${NC}."
echo -e "• Run ${CYAN}usbanywhere-ctl${NC} anytime to open the interactive Control Suite dashboard."
echo -e "• Run ${CYAN}usbanywhere-ctl doctor${NC} to perform a complete system health check."
echo -e "• Connect from your PC using ${CYAN}USBAnywhere Client${NC}!\n"
