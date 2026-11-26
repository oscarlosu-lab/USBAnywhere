# ⚡ USBAnywhere Automation & Setup Scripts

🌐 **Language / Idioma:** **English** | [Español](README_ES.md)

This directory contains provisioning scripts, control suites, and media automation tools for the USBAnywhere ecosystem.

[![License](https://img.shields.io/badge/License-GPLv3-blue.svg?style=flat-square)](https://www.gnu.org/licenses/gpl-3.0)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20Project-FF5E5B?style=flat-square&logo=ko-fi&logoColor=white)](https://ko-fi.com/oscarlosu)

---

## 📜 Script Index

### 1. `usbanywhere-ctl` — Unified Control Suite (macOS & Linux)
Interactive Terminal User Interface (TUI) dashboard and CLI management tool with zero external dependencies.
- **Interactive TUI Dashboard**: `usbanywhere-ctl` (or `usbanywhere-ctl dashboard`)
- **Dashboard Mode**: `usbanywhere-ctl mode [server|client|both]` (asked on first launch; switch anytime with `[m]`)
- **Optional Client Sleep Guard (Linux client)**: disabled by default. Installing the script, opening the dashboard and connecting devices do not install or enable any service. From the dashboard (`[g]`), you can choose **Disabled**, **Temporary** (active only while the dashboard is open, stops on exit), or **Permanent** (background systemd service, starts at boot). From the CLI: `sudo usbanywhere-ctl client-guard install|uninstall|status`. Ordinary connect/disconnect works without it. When active, it listens for logind `PrepareForSleep`, holds a sleep delay inhibitor, detaches imported USB/IP devices before suspend (verified in vhci_hcd sysfs) and re-attaches them on resume once the server answers on TCP 3240. Logs: `journalctl -u usbanywhere-client-guard`. Requires Linux with systemd/logind, `busctl`, `systemd-inhibit`, `udevadm` and `usbip`. Lid behavior is not configured by USBAnywhere.
- **System Doctor**: `usbanywhere-ctl doctor`
- **Protocol Loopback Test**: `usbanywhere-ctl test`
- **Service Control**: `sudo usbanywhere-ctl start|stop|restart|status`
- **Device Management**: `usbanywhere-ctl list`, `sudo usbanywhere-ctl share <busid>`
- **Linux Client Mode (`vhci_hcd`)**: `usbanywhere-ctl scan|remote <ip>`, `sudo usbanywhere-ctl connect|disconnect|ports`
- **Security Policy (`TCP 3241`)**: `sudo usbanywhere-ctl policy show|allow|deny|protect-hid|allow-ip|deny-ip|set-token|clear-token`
- **DualSense SBC Fix**: `sudo usbanywhere-ctl fix-dualsense enable|disable|status`

### 2. `install-server-mac.sh` — 1-Click macOS Server Installer
Automated installer for macOS (Apple Silicon M1-M4 & Intel x86_64).
- Installs native Swift server daemon and LaunchDaemon service.
- Installs `usbanywhere-ctl` to `/usr/local/bin/`.

```bash
curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere-Server/main/scripts/install-server-mac.sh | sudo bash
```

### 3. `install-server-linux.sh` / `install-server.sh` — 1-Click Linux / Raspberry Pi Installer
Automated installer for Linux distributions and Raspberry Pi OS (Debian/Ubuntu/Raspbian/Arch/Fedora).
- Automatically loads `usbip_core` and `usbip_host` kernel modules.
- Installs `usbip` userspace package and systemd daemon.
- Configures udev hotplug auto-exporting rule.
- Installs `usbanywhere-ctl` to `/usr/local/bin/`.

```bash
curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere-Server/main/scripts/install-server-linux.sh | sudo bash
```

---

## 📄 License

GPL-3.0 License. Developed by [oscarlosu-lab](https://github.com/oscarlosu-lab).
