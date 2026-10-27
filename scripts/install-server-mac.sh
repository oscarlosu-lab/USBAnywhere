#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# USBAnywhere Server for macOS — 1-Click Automated Installer
# Copyright (c) 2025-2026 oscarlosu-lab - https://github.com/oscarlosu-lab/USBAnywhere-Server
# Licensed under GPL v3
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

echo -e "${CYAN}${BOLD}"
echo "╔══════════════════════════════════════════════════════════════════════╗"
echo "║          🌐 USBAnywhere Server for macOS — 1-Click Auto-Setup        ║"
echo "║      Share Mac USB Peripherals (Joysticks, Wheels, Audio, Dongles)   ║"
echo "╚══════════════════════════════════════════════════════════════════════╝"
echo -e "${NC}"

# 1. Require macOS
if [[ "$(uname -s)" != "Darwin" ]]; then
    echo -e "${RED}❌ Error: This installer is intended exclusively for macOS.${NC}"
    exit 1
fi

# 2. Require root
if [[ "$EUID" -ne 0 ]]; then
    echo -e "${YELLOW}⚠️ Root privileges required. Re-running with sudo...${NC}"
    exec sudo bash "$0" "$@"
fi

ARCH=$(uname -m)
echo -e "${CYAN}🔍 Detected architecture: ${BOLD}${ARCH}${NC}"

INSTALL_DIR="/usr/local/bin"
PLIST_PATH="/Library/LaunchDaemons/com.usbanywhere.server.plist"
BINARY_TARGET="${INSTALL_DIR}/usbanywhere-server-mac"
LOG_FILE="/var/log/usbanywhere-server.log"

mkdir -p "${INSTALL_DIR}"

# 3. Build or download binary
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOURCE_DIR="$(cd "${SCRIPT_DIR}/../src/USBAnywhere.Server.Mac" 2>/dev/null && pwd || true)"

if [[ -d "${SOURCE_DIR}" && -f "${SOURCE_DIR}/Package.swift" ]]; then
    echo -e "${CYAN}🔨 Building native Universal/macOS binary from source...${NC}"
    
    # Check for swift / Xcode tools
    if ! command -v swift &>/dev/null; then
        echo -e "${YELLOW}📦 Swift not found in PATH. Prompting for Xcode Command Line Tools...${NC}"
        xcode-select --install || true
        echo -e "${YELLOW}Please complete Xcode Command Line Tools installation and rerun this script.${NC}"
        exit 1
    fi
    
    cd "${SOURCE_DIR}"
    swift build -c release
    BUILT_BIN="${SOURCE_DIR}/.build/release/usbanywhere-server-mac"
    if [[ -f "${BUILT_BIN}" ]]; then
        cp "${BUILT_BIN}" "${BINARY_TARGET}"
        chmod +x "${BINARY_TARGET}"
        echo -e "${GREEN}✓ Binary compiled and installed to ${BINARY_TARGET}${NC}"
    fi
fi

# If binary is not yet in /usr/local/bin, download pre-built release binary
if [[ ! -f "${BINARY_TARGET}" ]]; then
    echo -e "${CYAN}⬇️ Fetching official release binary for ${ARCH}...${NC}"
    RELEASE_URL="https://github.com/oscarlosu-lab/USBAnywhere-Server/releases/latest/download/usbanywhere-server-mac"
    if curl -fsSL -o "${BINARY_TARGET}" "${RELEASE_URL}"; then
        chmod +x "${BINARY_TARGET}"
        echo -e "${GREEN}✓ Downloaded and installed to ${BINARY_TARGET}${NC}"
    else
        echo -e "${RED}❌ Could not compile or download binary.${NC}"
        exit 1
    fi
fi

# 4. Create Launchd Service
echo -e "${CYAN}⚙️ Configuring LaunchDaemon service...${NC}"
cat <<EOF > "${PLIST_PATH}"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.usbanywhere.server</string>
    <key>ProgramArguments</key>
    <array>
        <string>${BINARY_TARGET}</string>
        <string>serve</string>
        <string>--auto-bind</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>${LOG_FILE}</string>
    <key>StandardErrorPath</key>
    <string>${LOG_FILE}</string>
</dict>
</plist>
EOF

chown root:wheel "${PLIST_PATH}"
chmod 644 "${PLIST_PATH}"

# Reload service
launchctl unload "${PLIST_PATH}" 2>/dev/null || true
launchctl load -w "${PLIST_PATH}"

echo -e "${GREEN}✓ LaunchDaemon loaded and running at boot.${NC}"

# 5. Install unified Control Suite (usbanywhere-ctl)
echo -e "${CYAN}🎛️ Installing USBAnywhere Control Suite...${NC}"
CTL_TARGET="/usr/local/bin/usbanywhere-ctl"
if [[ -f "${SCRIPT_DIR}/usbanywhere-ctl" ]]; then
    cp "${SCRIPT_DIR}/usbanywhere-ctl" "${CTL_TARGET}"
    chmod +x "${CTL_TARGET}"
else
    curl -fsSL "https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere-Server/main/scripts/usbanywhere-ctl" -o "${CTL_TARGET}" || true
    chmod +x "${CTL_TARGET}" 2>/dev/null || true
fi
echo -e "${GREEN}✓ Control Suite installed to ${CTL_TARGET}.${NC}"

# 6. Display success and instructions
IP_ADDR=$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || echo "127.0.0.1")

echo ""
echo -e "${GREEN}${BOLD}══════════════════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}${BOLD}  🎉 USBAnywhere Server for macOS is Active & Sharing Devices!        ${NC}"
echo -e "${GREEN}${BOLD}══════════════════════════════════════════════════════════════════════${NC}"
echo -e "  📡 Server IP:     ${CYAN}${BOLD}${IP_ADDR}:3240${NC}"
echo -e "  📁 Service Plist: ${CYAN}${PLIST_PATH}${NC}"
echo -e "  📜 Service Logs:  ${CYAN}${LOG_FILE}${NC}"
echo ""
echo -e "${BOLD}Next Steps:${NC}"
echo -e "  1. Open ${CYAN}USBAnywhere Client${NC} on your Windows PC."
echo -e "  2. Enter your Mac IP: ${CYAN}${IP_ADDR}${NC} and click ${CYAN}List / Connect${NC}."
echo -e "  3. To check connected devices from Terminal: ${CYAN}usbanywhere-server-mac list${NC}"
echo ""
