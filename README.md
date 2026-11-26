# 🌐 USBAnywhere — The Free, Open-Source VirtualHere Alternative for Windows

🌐 **Language / Idioma:** **English** | [Español](README_ES.md)

**v3.5.0** | [🌍 Official Website](https://oscarlosu-lab.github.io/USBAnywhere/) | [📦 Releases & Downloads](https://github.com/oscarlosu-lab/USBAnywhere/releases/latest) | [🌐 USBAnywhere Server (Windows & macOS)](https://github.com/oscarlosu-lab/USBAnywhere-Server) | [📱 USBAnywhere Android](https://github.com/oscarlosu-lab/USBAnywhere-Android) | [💬 Reddit Community](https://www.reddit.com/r/UsbAnywhere/) | [📖 User Manual](docs/USAGE_EN.md) | [🌐 VPN Connection](docs/VPN_INTERNET_EN.md)

> **Why pay $49.00 per server and be restricted to a single USB device?**
> **USBAnywhere** connects **unlimited** remote USB peripherals over LAN, Wi-Fi, and Internet with **zero licensing fees**, **Microsoft WHQL-certified drivers**, and **tunable ultra-low latency**.

Transform any Linux device (or Windows PC) into a high-performance Virtual USB Hub accessible from Windows 10 and 11. Compatible with **Raspberry Pi, Orange Pi, Banana Pi, Proxmox/HomeLab, Steam Deck, OpenWRT routers, CrealityBox**, and any ARM/x86 Linux server.

[![CI Build & Tests](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/ci.yml)
[![CodeQL SAST](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/codeql.yml)
[![Gitleaks Secret Scan](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/security.yml/badge.svg?branch=main)](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/security.yml)
[![SignPath Release Signing](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/sign-release.yml/badge.svg?branch=main)](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/sign-release.yml)
[![GitHub Pages](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/pages.yml/badge.svg?branch=main)](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/pages.yml)
[![Latest Release](https://img.shields.io/github/v/release/oscarlosu-lab/USBAnywhere?style=flat-square&logo=github&color=0ea5e9)](https://github.com/oscarlosu-lab/USBAnywhere/releases/latest)
[![GitHub Downloads](https://img.shields.io/github/downloads/oscarlosu-lab/USBAnywhere-Downloads/total?style=flat-square&logo=github&color=blue)](https://github.com/oscarlosu-lab/USBAnywhere-Downloads/releases)
[![Last Commit](https://img.shields.io/github/last-commit/oscarlosu-lab/USBAnywhere?style=flat-square&color=10b981)](https://github.com/oscarlosu-lab/USBAnywhere/commits/main)
[![Issues Closed](https://img.shields.io/github/issues-closed/oscarlosu-lab/USBAnywhere?style=flat-square&color=22c55e)](https://github.com/oscarlosu-lab/USBAnywhere/issues?q=is%3Aissue+is%3Aclosed)
[![Security Policy](https://img.shields.io/badge/Security-Policy%20%26%20Zero--Trust-10b981?style=flat-square&logo=shield&logoColor=white)](SECURITY.md)
[![Code Signing](https://img.shields.io/badge/Code%20Signing-SignPath%20Foundation-6366f1?style=flat-square&logo=letsencrypt&logoColor=white)](CODE_SIGNING_POLICY.md)
[![Microsoft WHQL](https://img.shields.io/badge/Drivers-Microsoft%20WHQL%20%7C%20HVCI-0078D6?style=flat-square&logo=windows&logoColor=white)](https://github.com/oscarlosu-lab/USBAnywhere)
[![SHA-256 Verified](https://img.shields.io/badge/Checksums-SHA--256%20Verified-14b8a6?style=flat-square)](https://github.com/oscarlosu-lab/USBAnywhere/releases/latest)
[![GitHub Stars](https://img.shields.io/github/stars/oscarlosu-lab/USBAnywhere?style=flat-square&logo=github&color=yellow)](https://github.com/oscarlosu-lab/USBAnywhere/stargazers)
[![License](https://img.shields.io/badge/License-GPLv3-blue.svg?style=flat-square)](https://www.gnu.org/licenses/gpl-3.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20(x64%20%26%20ARM64)-lightgrey?style=flat-square&logo=windows)](https://github.com/oscarlosu-lab/USBAnywhere)
[![Reddit Community](https://img.shields.io/badge/Reddit-r%2FUsbAnywhere-FF4500?style=flat-square&logo=reddit&logoColor=white)](https://www.reddit.com/r/UsbAnywhere/)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20Project-FF5E5B?style=flat-square&logo=ko-fi&logoColor=white)](https://ko-fi.com/oscarlosu)

---

## 📦 Which Repository Do I Need?

| Repository | Role | Where to Install |
| :--- | :--- | :--- |
| **[USBAnywhere](https://github.com/oscarlosu-lab/USBAnywhere)** *(this repo)* | **Client** (Windows WPF + WHQL drivers & Linux CLI) + Linux/Raspberry Pi headless server bootstrap (`scripts/install-server.sh`) | On the **PC using the remote USB device** (and Linux/Pi hosts) |
| **[USBAnywhere-Server](https://github.com/oscarlosu-lab/USBAnywhere-Server)** | **Windows & macOS Host Server** (GUI/tray app to export local USB devices) | Only on the **Windows/Mac PC where the USB device is physically plugged in** |
| **[USBAnywhere-Android](https://github.com/oscarlosu-lab/USBAnywhere-Android)** | **Android & Android TV** client & rootless OTG host | On Android phones, tablets, Nvidia Shield, or Meta Quest |
| **[SnakeUSBIP](https://github.com/oscarlosu-lab/SnakeUSBIP)** | **Staging / Pre-Release Edition** *(migrating into USBAnywhere)* | Experimental branch where unverified improvements are tested first; **USBAnywhere** is the final production product |

---

## 🧭 Project focus

USBAnywhere is a feature-rich desktop and networking solution designed to simplify USB-over-IP access for Windows environments.

The project continues to evolve through a mix of usability, interoperability, and operational reliability improvements. Engineering effort is centered on strengthening platform compatibility, release quality, user experience, and long-term maintainability.

---

## ⚔️ Why Switch from VirtualHere to USBAnywhere?

| Feature | **USBAnywhere v3.4.1** | **VirtualHere** | **FlexiHub** | **USB Network Gate** |
| :--- | :---: | :---: | :---: | :---: |
| **Price & License** | **100% Free & Open Source (GPLv3)** | $49.00 USD / Server | ~$15–$30+ / mo SaaS | $159.00+ USD |
| **Simultaneous Devices (Free)** | **♾️ Unlimited** | ⚠️ 1 Device Only | ❌ 20-min Trial / 1 Conn | ❌ Trial Expired |
| **Account & Cloud Privacy** | **✅ 100% Offline / Zero Account** | ✅ Local License | ❌ Mandatory Cloud Login | ✅ Offline License |
| **1-Click Linux Server Setup** | ✅ **Built-in SSH & `udev` Hotplug Auto-Installer** | ❌ Manual CLI setup | ⚠️ Closed Binary Daemon | ❌ Proprietary daemon |
| **Windows Kernel Communication** | ✅ **Direct Native VHCI IOCTL + Push Events** | ✅ Native Client/Driver | ✅ Proprietary Service | ✅ Proprietary Service |
| **Latency & Network Modes** | ✅ **Dual: Low-Latency WSK & Zero-Copy** | ⚙️ Generic | ⚙️ Proprietary Relay | ⚙️ Generic |
| **Global Hotkey & Headless CLI** | ✅ **`Ctrl+Alt+D` + `--disconnect-all`/`--quit`** | ❌ GUI Only / Basic | ❌ Closed GUI | ⚠️ Paid SDK |
| **User Interface** | ✅ **Modern WPF (Dark & Light Themes + `[ms]` Ping)** | 🕰️ Legacy Win98 TreeView | 💻 Account-Gated App | 💻 Heavy Commercial UI |
| **Drivers (Windows 10/11)** | ✅ **Microsoft WHQL Certified (HVCI Ready)** | ✅ Signed | ✅ Signed | ✅ Signed |
| **Native VPN Integration** | ✅ **Built-in Tailscale, ZeroTier & WireGuard** | 💰 Extra / Manual | ⚠️ Cloud Relay Server | ❌ Manual |

---

## 🎯 Built for the Communities Left Behind by Paywalls

1. 🎮 **Cloud Gaming, Moonlight / Sunshine & Steam Deck**
   Pass Xbox/DualSense controllers (with full rumble, adaptive triggers, and audio), wireless headset dongles, and keyboards from your living room Raspberry Pi or handheld directly to your gaming rig.
2. 🏎️ **SimRacing & Flight Simulators (HOTAS / VR)**
   Connect steering wheels, load-cell pedals, shifters, button boxes, and VR trackers simultaneously over Ethernet using **Low-Latency WSK mode** without hitting a 1-device paywall.
3. 🖨️ **3D Printing (Klipper, Mainsail, OctoPrint, CrealityBox)**
   Bridge multiple USB webcams, ADXL345 resonance accelerometers, and printer mainboards effortlessly.
4. 🖥️ **HomeLab, Proxmox & Self-Hosted**
   Attach Zigbee/Z-Wave coordinators, hardware security keys, and license dongles (AutoCAD, iLok, eToken) over LAN or Tailscale VPN.
5. 🥽 **Virtual Reality & Facial Tracking (HTC Vive Facial Tracker, VRChat, Quest)**
   Stream uncompressed 60 FPS dual IR camera streams into VRCFaceTracking (VRCFT) with **Zero-Copy mode** and **Start Minimized to Tray** automation without the stuttering or disconnects common in VirtualHere. 👉 **[Read the VR Facial Tracker Optimization Guide](docs/VR_FACIAL_TRACKER_GUIDE_EN.md)**.

---

## 🎬 Video Tutorials & Guides (YouTube)

### 🌟 Full Masterclass Tutorial (The FREE VirtualHere Alternative)
<a href="https://www.youtube.com/watch?v=gKoUBd5_lj8">
  <img src="https://img.youtube.com/vi/gKoUBd5_lj8/maxresdefault.jpg" width="70%" alt="USBAnywhere: The FREE VirtualHere Alternative (Full Masterclass Tutorial)">
</a>

▶️ **[Watch Full Masterclass Tutorial on YouTube](https://www.youtube.com/watch?v=gKoUBd5_lj8)**

### 🔥 Diode Laser & LightBurn Camera Setup Guide
Connect your diode laser (GRBL/Ruida) and overhead alignment camera simultaneously over Wi-Fi or VPN with zero mid-burn disconnects and zero paywalls:

<a href="https://www.youtube.com/watch?v=lyqBBE8xdFQ">
  <img src="https://img.youtube.com/vi/lyqBBE8xdFQ/maxresdefault.jpg" width="70%" alt="Connect Diode Laser & Camera to LightBurn Wirelessly over Wi-Fi / VPN">
</a>

▶️ **[Watch Diode Laser & LightBurn Camera Tutorial on YouTube](https://www.youtube.com/watch?v=lyqBBE8xdFQ)**

---

## 🆕 What's New in v3.5.0

- 📌 **Kernel Boot Persistence & Retry Control (`VhciNativeClient.cs`)** - Save attached devices in the `usbip2_ude` kernel driver to re-attach automatically on Windows boot, or stop active kernel attach retries from the context menu.
- 🏷️ **Custom USB Serial Numbers & Virtual Hub Ports (`KernelConfigService.cs`)** - Assign persistent custom `Serial` strings per remote device and configure Virtual Hub USB 2.0 / USB 3.0 port capacity (`30` / `30` default).
- 🩺 **One-Click Diagnostics Bundle (`KernelDiagnosticsService.cs`)** - Export a `.zip` archive with driver status, imported VHCI ports, Virtual Hub configuration, and recent logs.
- 🔒 **Linux Control API Hardening & UAS Detection (`usbanywhere-ctl` v3.5.0)** - Optional IP allowlist (`allowed_ips`) and constant-time HMAC `control_token` authentication (`policy allow-ip|deny-ip|set-token|clear-token`) on TCP `3241`, plus `uas` external SSD compatibility notices.

## 🆕 What's New in v3.4.1 (USBAnywhere Multiplatform Suite)

- 🔗 **Single-Instance IPC Forwarding (`--connect` / `--attach`)** - Launching `USBAnywhere.exe --connect` or `--attach` forwards the request directly to the already-running instance via named IPC pipe instead of spawning a duplicate window.

- ⚡ **Direct Kernel VHCI IOCTL & Real-Time Push Events (`VhciNativeClient.cs`)** - Communicates directly with the `usbip2_ude` Windows kernel driver via Win32 `DeviceIoControl` and async overlapped `ReadFile` events (eliminating repeated `usbip.exe` process spawns, regex parsing, and per-call SHA-256 checks, with clean CLI fallback and Windows 11 tray icon sizing).
- ⌨️ **Global Hotkey (`Ctrl+Alt+D`) & Headless CLI Controls** - Instantly release all remote USB peripherals via configurable hotkey, tray `Disconnect All`, or headless CLI flags (`--disconnect-all`, `--disconnect <port>`, `--quit`).
- 🐧 **Universal Linux Client Mode (`usbanywhere-ctl` v3.4.1)** - Full client support on Linux (SteamOS, Bazzite, Ubuntu, Debian, Mint, Arch, Fedora) via `vhci_hcd` with automatic **On-Demand Dynamic Binding (`TCP 3241` `LIST`/`BIND`/`UNBIND`)** (`scan`, `remote`, `connect`, `disconnect`, `ports` + interactive Dashboard `[9]` / `[0]`).
- ⌨️ **Android USB Keyboard, String Fallback & Pure HID Enumeration Fix (`UrbSession.kt`)** - Accurate Full/Low/High-Speed deduction (`deduceSpeed`), `URB_SHORT_NOT_OK` clearance, `rawDescriptors` fallback (`0x01`/`0x02`/`0x03`/`0x21`), `bInterval == 0` sanitizer, synthesized `0x22` HID Report Descriptors (Boot Keyboard/Mouse/Gamepad), and benign `SET_IDLE`/`SET_PROTOCOL` completion for Windows UDE clients.
- 📺 **Android TV / Nvidia Shield D-Pad & Gamepad Navigation (`TvFocus.kt`)** - Full remote-control and gamepad focus navigation across all tabs, switches, and device cards.
- 🎮 **DualSense Audio Interface De-Authorization & Raspberry Pi `dwc_otg` Diagnostics** - Class-based `udev` rule (`ENV{DEVTYPE}=="usb_interface", ATTRS{idVendor}=="054c", ATTR{bInterfaceClass}=="01", ATTR{authorized}="0"`) preventing `usbip_host` isochronous audio panics across all DualSense (`0ce6`/`0df2`) firmware revisions.
- ⚡ **1-Click SSH & Plug & Play Server Auto-Setup** - Turn any Raspberry Pi, Debian, or Ubuntu machine into a ready-to-use USB over IP server with automatic On-Demand dynamic export.
- 🏎️ **Interactive Dual Performance Modes (`⚡ Mode: Zero-Copy` / `🏎️ Mode: Low-Latency`)** - Switch between `zero-copy` (high bandwidth storage/video) and `low-latency` (fast WSK event callbacks for HID/gaming/SimRacing) directly from the status bar.
- 🛡️ **Certified WHQL Drivers** - Official Microsoft-certified drivers for Windows 10/11 x64 with full Core Isolation (HVCI) support.
- 📦 **Hybrid Portable & Installer Mode** - ZIP releases store config locally; the hardened Inno Setup installer uses `%AppData%\USBAnywhere` and supports quiet `--minimized` Windows startup.

---

## ✨ Key Features

- 🔍 **Auto-Discovery** - Scan for USB/IP servers across your local network in seconds with live `[ms]` latency
- ⚡ **1-Click SSH Provisioning** - Install and start `usbipd` on your Raspberry Pi/Linux server automatically
- 🌐 **Internet Connection** - Connect anywhere via Tailscale, ZeroTier, or WireGuard (NAT traversal)
- 🏎️ **Tunable Latency** - Status-bar toggle between Zero-Copy throughput and Low-Latency WSK packet handling
- 🔌 **One-Click Connect & Auto-Disconnect** - Connect/disconnect devices cleanly
- ⭐ **Favorites & Auto-Reconnect** - Save devices with automatic 3-attempt reconnection and per-device latency profiles
- 📋 **Detailed Hardware Info & Renaming** - Real-time VID:PID, vendor lookup for 25,000+ USB devices, and custom device names
- 🎨 **Modern Native GUI** - Fast .NET 9 WPF interface with live Dark and Light themes
- 🌐 **Multi-language** - Instant switching between English and Spanish
- 🔄 **Secure Auto-Update** - Verifies GitHub releases with mandatory SHA-256 cryptographic checks (`SHA256SUMS.txt`)

---

## 📦 Installation

### Option 1: Portable (No installation required)
1. Download from [USBAnywhere Releases (v3.5.0)](https://github.com/oscarlosu-lab/USBAnywhere/releases/tag/v3.5.0):
   - **Windows x64**: `USBAnywhere-v3.4.1-x64.zip`
   - **Windows ARM64**: `USBAnywhere-v3.4.1-arm64.zip` (Surface Pro X, Snapdragon X Elite, etc.)
   - **Linux Client & Server Suite**: Run [`scripts/install-server.sh`](scripts/install-server.sh) (see 1-line installer below)
2. Extract the ZIP to any folder
3. Run `USBAnywhere.exe` as Administrator
4. Done!

### Option 2: Installer (x64 Recommended)
1. Download `USBAnywhere_Setup_v3.5.0.exe` from [USBAnywhere Releases (v3.5.0)](https://github.com/oscarlosu-lab/USBAnywhere/releases/tag/v3.5.0)
2. Run the installer as Administrator
3. Follow the installation wizard

### ⚠️ ARM64 Users
ARM64 drivers are **test-signed**. See `README_ARM64.md` in the ZIP for instructions to enable Windows Test Mode.

---

## 🚀 Quick Start

1. **Scan** - Click `🔍 Scan` to find servers on your network
2. **List** - Click `📋 List` to see available USB devices
3. **Connect** - Double-click a device or right-click → Connect
4. **Disconnect** - Right-click → Disconnect

### ⚡ 1-Click Linux / Raspberry Pi Server Setup
1. Click `🔗 SSH` in the top toolbar
2. Enter your Raspberry Pi / Linux username (e.g. `pi` or `root`) and IP
3. Click **`⚡ 1-Click Auto-Setup`** — it automatically installs `usbip`, loads kernel modules persistently, starts the daemon, and lists your USB ports!

**Or run our 1-Line Plug & Play Installer on Linux / Raspberry Pi:**
```bash
curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere/main/scripts/install-server.sh | sudo bash
```
> 💡 **100% Plug & Play (Like VirtualHere!):** Installs `usbipd`, creates a `systemd` service, and adds a **`udev` hotplug rule** that **automatically exports any USB device as soon as you plug it into your Raspberry Pi/Linux server** — zero manual `usbip bind` commands required!

### 🐳 Docker / HomeLab / NAS Server (`docker-compose.yml`)
Run a Plug & Play USB/IP server on **Unraid, TrueNAS, Synology, or Proxmox** using [`docker/docker-compose.yml`](docker/docker-compose.yml):
```bash
git clone --recursive https://github.com/oscarlosu-lab/USBAnywhere.git && cd USBAnywhere/docker && docker compose up -d
```

### 🐧 Linux Client Mode (`usbanywhere-ctl` — SteamOS, Bazzite, Ubuntu, Mint, Arch, Fedora)
Connect remote USB devices from any USBAnywhere server (Android, Raspberry Pi, Linux, Windows, macOS) directly to a **Linux client** using `usbanywhere-ctl` (with full On-Demand Dynamic Binding `TCP 3241` + `vhci_hcd` support):
```bash
sudo usbanywhere-ctl scan                  # Discover USBAnywhere servers on LAN & Tailscale/VPN
sudo usbanywhere-ctl remote 192.168.1.50   # List exported + On-Demand USB devices on a remote server
sudo usbanywhere-ctl connect 192.168.1.50 1-1.4   # Dynamically bind & attach a remote USB device
sudo usbanywhere-ctl ports                 # List currently attached virtual USB client ports
sudo usbanywhere-ctl disconnect all        # Detach client port(s) & release back to remote host
sudo usbanywhere-ctl policy allow-ip 192.168.1.50  # Restrict TCP 3241 control access by IP (optional)
sudo usbanywhere-ctl policy set-token <secret>     # Require HMAC token for TCP 3241 commands (optional)
```

### 🎮 Moonlight / Sunshine / Playnite CLI Automation
Automate controller, wheel, or headset attachment when starting a game stream:
```powershell
USBAnywhere.exe --minimized --connect 192.168.1.50 1-1.4
```

### 🌐 Internet Connection (VPN)
1. Install **[Tailscale](https://tailscale.com/download)** on Windows and your server
2. Click `● VPN` to scan peers with active USB/IP (port 3240)
3. Select remote server(s) and click Connect

See [docs/VPN_INTERNET_EN.md](docs/VPN_INTERNET_EN.md) for the complete guide.

---

## 🐧 Manual Linux Server Setup (Optional)

Works on **any Linux device** with USB ports (Raspberry Pi, Orange Pi, Banana Pi, OpenWRT, CrealityBox, Debian/Ubuntu x86/ARM):

**Debian / Ubuntu / Raspberry Pi OS:**
```bash
sudo apt update && sudo apt install -y usbip hwdata usbutils
sudo modprobe usbip_host
sudo usbipd -D
sudo usbip list -l
sudo usbip bind -b 1-1.4  # Replace with your bus-id
```

**OpenWRT:**
```bash
opkg update && opkg install usbip-server kmod-usb-ohci
usbipd -D
```

See [docs/RASPBERRY_PI_SERVER_EN.md](docs/RASPBERRY_PI_SERVER_EN.md) for complete instructions.

---

## 📚 Documentation & Technical Manuals

| Guide | Description | Language |
|---|---|---|
| **[Security Policy](SECURITY.md)** | Zero-Trust defense, WHQL drivers & Authenticode validation | [English](SECURITY.md) · [Español](SECURITY_ES.md) |
| **[Windows Client Manual](docs/USAGE_EN.md)** | Comprehensive UI navigation, latency modes & shortcuts | [English](docs/USAGE_EN.md) · [Español](docs/USAGE_ES.md) |
| **[Desktop Server Guide](server-windows/README.md)** | Unified Windows (.NET 9) & macOS (Swift) server suite | [English](server-windows/README.md) · [Español](server-windows/README_ES.md) |
| **[macOS Server Manual](server-windows/docs/USAGE_MACOS_EN.md)** | Native Swift server setup for Apple Silicon M1-M4 & Intel | [English](server-windows/docs/USAGE_MACOS_EN.md) · [Español](server-windows/docs/USAGE_MACOS_ES.md) |
| **[Raspberry Pi / Linux](docs/RASPBERRY_PI_SERVER_EN.md)** | 1-Click setup, systemd service & udev hotplug auto-export | [English](docs/RASPBERRY_PI_SERVER_EN.md) · [Español](docs/RASPBERRY_PI_SERVER_ES.md) |
| **[Docker Server Guide](docker/README.md)** | Privileged container deployment & Docker Compose | [English](docker/README.md) · [Español](docker/README_ES.md) |
| **[VPN & Internet Setup](docs/VPN_INTERNET_EN.md)** | Tailscale, ZeroTier & WireGuard remote gaming guide | [English](docs/VPN_INTERNET_EN.md) · [Español](docs/VPN_INTERNET_ES.md) |
| **[Windows ARM64 Guide](docs/README_ARM64.md)** | Test-signing instructions for Snapdragon X Elite / Surface Pro | [English](docs/README_ARM64.md) |

---

---

## 🔐 Code Signing

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

Official Windows binaries are cryptographically signed using SignPath Foundation to verify their authenticity, provenance, and integrity. Read our full [Code Signing Policy](CODE_SIGNING_POLICY.md).

---

## 📄 License

This project is licensed under the **GNU General Public License v3.0 (GPLv3)**.

> **USBAnywhere is 100% Free and Open Source.** You are free to run, study, share, and modify the software under the terms of the GNU General Public License as published by the Free Software Foundation. **GPLv3 copyleft prohibits closed-source proprietary commercial reuse** and requires preserving all copyright notices with prominent attribution to **oscarlosu-lab** (`https://github.com/oscarlosu-lab`) for any full or partial reuse.

See [LICENSE](LICENSE) for full legal terms and conditions.

## 🔗 USBAnywhere Ecosystem

| Component | Platform / Role | Description |
|---|---|---|
| **[USBAnywhere Client](https://github.com/oscarlosu-lab/USBAnywhere)** | 🖥️ Windows 10/11 (x64 / ARM64) | Connect to remote USB devices with WHQL drivers & WSK engine |
| **[USBAnywhere Server](https://github.com/oscarlosu-lab/USBAnywhere-Server)** | 🌐 Windows & 🍎 macOS | Desktop USB/IP server with GUI (Windows) & Swift daemon (macOS) |
| **[USBAnywhere Android](https://github.com/oscarlosu-lab/USBAnywhere-Android)** | 📱 Android & Android TV | Rootless USB/IP server, Sunshine handoff & DualSense haptics |
| **[Official Website](https://oscarlosu-lab.github.io/USBAnywhere/)** | 🌍 Web Portal | Interactive guides, documentation & download hub |
| **[Reddit Community](https://www.reddit.com/r/UsbAnywhere/)** | 💬 r/UsbAnywhere | Support, discussions, community setups & announcements |
| **[Support on Ko-fi](https://ko-fi.com/oscarlosu)** | ☕ Community | Support hosting, driver testing & open-source engineering |

---

## ☕ Support the Project

If USBAnywhere helped your sim rig, cloud gaming, or 3D printing setup, consider [buying us a coffee on Ko-fi](https://ko-fi.com/oscarlosu). Your contributions directly fund driver code-signing certificates, test hardware (SBCs, controllers, wheelbases), and hosting.

---

## 🙏 Credits

| Project | Author | Contribution |
|---------|--------|--------------|
| [usbip-win2](https://github.com/vadimgrn/usbip-win2) | **Vadim Grn** | Microsoft-signed USB/IP drivers (WHQL certified). Core of the Windows client. |
| [OctoWrt](https://github.com/ihrapsa/OctoWrt) | **ihrapsa** | Original OpenWrt guide for CrealityBox. Inspiration for embedded device support. |
| [OctoWrt Fork](https://github.com/shivajiva101/OctoWrt) | **ShivaJiva** | Active OctoWrt maintenance. Updated releases for CrealityBox. |
| [USB/IP](https://www.kernel.org/doc/html/latest/usb/usbip_protocol.html) | **Linux Kernel** | Original USB/IP protocol |
| **USBAnywhere** | **SnakeFoxu (OscarLosu)** | .NET 9 WPF GUI, 1-Click SSH setup, VPN integration, latency modes & documentation |
