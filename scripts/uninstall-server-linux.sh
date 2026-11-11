#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# USBAnywhere Server for Linux & Raspberry Pi - Uninstaller
# Copyright (c) 2025-2026 oscarlosu-lab - https://github.com/oscarlosu-lab/USBAnywhere-Server
# Licensed under GPL v3
# ─────────────────────────────────────────────────────────────────────────────

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m'

if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}[✗] Please run as root (use sudo).${NC}"
    exit 1
fi

echo -e "${BLUE}[1/4] Stopping and disabling usbipd service...${NC}"
systemctl stop usbipd 2>/dev/null || true
systemctl disable usbipd 2>/dev/null || true
rm -f /etc/systemd/system/usbipd.service
systemctl daemon-reload

echo -e "${BLUE}[2/4] Removing udev hotplug rules...${NC}"
rm -f /etc/udev/rules.d/99-usbip-autobind.rules
udevadm control --reload-rules 2>/dev/null || true

echo -e "${BLUE}[3/4] Removing modules configuration...${NC}"
rm -f /etc/modules-load.d/usbip.conf

echo -e "${BLUE}[4/4] Removing Control Suite executable...${NC}"
rm -f /usr/local/bin/usbanywhere-ctl

echo -e "${GREEN}✓ USBAnywhere Linux Server & Control Suite removed cleanly.${NC}"
