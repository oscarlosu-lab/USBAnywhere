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

if [ "${EUID:-$(id -u)}" -ne 0 ]; then
    echo -e "${RED}[✗] Please run as root (use sudo).${NC}"
    exit 1
fi

echo -e "${BLUE}[1/5] Stopping and disabling usbipd service...${NC}"
systemctl stop usbipd 2>/dev/null || true
systemctl disable usbipd 2>/dev/null || true
rm -f /etc/systemd/system/usbipd.service

echo -e "${BLUE}[2/5] Stopping and disabling Dynamic Auto-Unbind & legacy autobind daemons...${NC}"
systemctl stop usbanywhere-autounbind 2>/dev/null || true
systemctl disable usbanywhere-autounbind 2>/dev/null || true
rm -f /etc/systemd/system/usbanywhere-autounbind.service
systemctl stop usbanywhere-autobind 2>/dev/null || true
systemctl disable usbanywhere-autobind 2>/dev/null || true
rm -f /etc/systemd/system/usbanywhere-autobind.service
rm -f /usr/local/bin/usbanywhere-autobind.sh

echo -e "${BLUE}[3/5] Removing Avahi discovery service, udev rules & export policies...${NC}"
rm -f /etc/avahi/services/usbanywhere.service
rm -f /etc/udev/rules.d/99-usbip-autobind.rules
rm -f /etc/udev/rules.d/99-usbanywhere-autobind.rules
rm -f /etc/udev/rules.d/99-usbanywhere-dualsense-fix.rules
rm -rf /etc/usbanywhere
systemctl restart avahi-daemon 2>/dev/null || true
udevadm control --reload-rules 2>/dev/null || true

echo -e "${BLUE}[4/5] Removing kernel modules configuration...${NC}"
rm -f /etc/modules-load.d/usbip.conf /etc/modules-load.d/usbanywhere.conf

echo -e "${BLUE}[5/5] Removing Control Suite executable...${NC}"
rm -f /usr/local/bin/usbanywhere-ctl

systemctl daemon-reload 2>/dev/null || true

echo -e "${GREEN}✓ USBAnywhere Linux Server & Control Suite removed cleanly.${NC}"
