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
- **System Doctor**: `usbanywhere-ctl doctor`
- **Protocol Loopback Test**: `usbanywhere-ctl test`
- **Service Control**: `sudo usbanywhere-ctl start|stop|restart|status`
- **Device Management**: `usbanywhere-ctl list`, `sudo usbanywhere-ctl share <busid>`

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
