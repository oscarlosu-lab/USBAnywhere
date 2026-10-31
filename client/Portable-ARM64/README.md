# 🌐 USBAnywhere — The Free, Open-Source VirtualHere Alternative for Windows

🌐 **Language / Idioma:** **English** | [Español](README_ES.md)

**v3.0.1** | [Download v3.0.1](https://github.com/oscarlosu-lab/USBAnywhere-Downloads/releases/tag/client-v3.0.1) | [🌐 USBAnywhere Server (Windows)](https://github.com/oscarlosu-lab/USBAnywhere-Server) | [📖 User Manual](docs/USAGE_EN.md) | [🌐 VPN Connection](docs/VPN_INTERNET_EN.md)

> **Why pay $49.00 per server and be restricted to a single USB device?**
> **USBAnywhere** connects **unlimited** remote USB peripherals over LAN, Wi-Fi, and Internet with **zero licensing fees**, **Microsoft WHQL-certified drivers**, and **tunable ultra-low latency**.

Transform any Linux device (or Windows PC) into a high-performance Virtual USB Hub accessible from Windows 10 and 11. Compatible with **Raspberry Pi, Orange Pi, Banana Pi, Proxmox/HomeLab, Steam Deck, OpenWRT routers, CrealityBox**, and any ARM/x86 Linux server.

[![CI Build & Tests](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/ci.yml/badge.svg)](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/ci.yml)
[![GitHub Downloads](https://img.shields.io/github/downloads/oscarlosu-lab/USBAnywhere/total?style=flat-square&logo=github&color=blue)](https://github.com/oscarlosu-lab/USBAnywhere/releases)
[![GitHub Stars](https://img.shields.io/github/stars/oscarlosu-lab/USBAnywhere?style=flat-square&logo=github&color=yellow)](https://github.com/oscarlosu-lab/USBAnywhere/stargazers)
[![License](https://img.shields.io/badge/license-Free%20for%20Personal%20%26%20Gaming-blue?style=flat-square)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20(x64%20%26%20ARM64)-lightgrey?style=flat-square&logo=windows)](https://github.com/oscarlosu-lab/USBAnywhere)

---

## ⚔️ Why Switch from VirtualHere to USBAnywhere?

| Feature | **USBAnywhere v3.0** | **VirtualHere** | **USB Network Gate** |
| :--- | :---: | :---: | :---: |
| **Price & License** | **100% Free for Personal & Gaming Use** | $49.00 USD / Server | $159.00+ USD |
| **Simultaneous Devices (Free)** | **♾️ Unlimited** | ⚠️ 1 Device Only | ❌ Trial Expired |
| **1-Click Linux Server Setup** | ✅ **Built-in SSH & `udev` Hotplug Auto-Installer** | ❌ Manual CLI setup | ❌ Proprietary daemon |
| **Latency & Network Modes** | ✅ **Dual: Low-Latency WSK & Zero-Copy** | ⚙️ Generic | ⚙️ Generic |
| **User Interface** | ✅ **Modern WPF (Dark & Light Themes + `[ms]` Ping)** | 🕰️ Legacy Win98 TreeView | 💻 Heavy Commercial UI |
| **Drivers (Windows 10/11)** | ✅ **Microsoft WHQL Certified (HVCI Ready)** | ✅ Signed | ✅ Signed |
| **Native VPN Integration** | ✅ **Built-in Tailscale, ZeroTier & WireGuard** | 💰 Extra / Manual | ❌ Manual |

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

---

## 🎬 Video Tutorials (English & Spanish - YouTube Edition)

<a href="https://youtu.be/tICyZ-5VeWc">
  <img src="docs/demo.gif" width="50%" alt="Video Tutorial">
</a>

▶️ **[Watch full tutorial on YouTube](https://youtu.be/tICyZ-5VeWc)**

- 🇺🇸 **[Watch / Download Full HD 1080p Video Tutorial (English MP4)](docs/youtube_tutorial_en.mp4)** • [Subtitles (.srt)](docs/youtube_tutorial_en.srt)
- 🇪🇸 **[Ver / Descargar Vídeo Tutorial Completo 1080p (Español MP4)](docs/youtube_tutorial_es.mp4)** • [Subtítulos (.srt)](docs/youtube_tutorial_es.srt)

---

## 🆕 What's New in v3.0.0 (USBAnywhere Evolution)

- 🌐 **Rebranded to USBAnywhere** - A modern identity built to be the #1 open-source alternative to VirtualHere and VirtualUSB, with automatic configuration migration from `SnakeUSBIP` v2.x.
- ⚡ **1-Click SSH & Plug & Play Server Auto-Setup** - Turn any Raspberry Pi, Debian, or Ubuntu machine into a ready-to-use USB over IP server with automatic `udev` hotplug device export.
- 🏎️ **Interactive Dual Performance Modes (`⚡ Mode: Zero-Copy` / `🏎️ Mode: Low-Latency`)** - Switch between `zero-copy` (high bandwidth storage/video) and `low-latency` (fast WSK event callbacks for HID/gaming/SimRacing) directly from the status bar.
- 📊 **Live Server Latency Meter (`[ms]`) & Custom Device Renaming (`✏️`)** - Real-time TCP ping indicator next to each discovered server and persistent friendly naming per device.
- 🔌 **Graceful Detach & Hardened Driver Lifecycle** - Automatically frees connected remote USB ports on exit/uninstall and safely manages the `ROOT\USBIP_WIN2\UDE` virtual controller without duplicate nodes or forced USB root hub resets.
- 🛡️ **Certified WHQL Drivers** - Official Microsoft-certified drivers for Windows 10/11 x64 with full Core Isolation (HVCI) support.
- 📦 **Hybrid Portable & Installer Mode** - ZIP releases store config locally; the hardened Inno Setup installer uses `%AppData%\USBAnywhere` and supports quiet `--minimized` Windows startup.
- 📚 **Updated USB Database** - Latest `usb.ids` with 25,000+ recognized devices.

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
1. Download from [USBAnywhere Downloads](https://github.com/oscarlosu-lab/USBAnywhere-Downloads/releases/tag/client-v3.0.1):
   - **Windows x64**: `USBAnywhere-v3.0.1-x64.zip`
   - **Windows ARM64**: `USBAnywhere-v3.0.1-arm64.zip` (Surface Pro X, Snapdragon X Elite, etc.)
2. Extract the ZIP to any folder
3. Run `USBAnywhere.exe` as Administrator
4. Done!

### Option 2: Installer (x64 Recommended)
1. Download `USBAnywhere_Setup_v3.0.1.exe` from [USBAnywhere Downloads](https://github.com/oscarlosu-lab/USBAnywhere-Downloads/releases/tag/client-v3.0.1)
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
curl -sSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere/main/scripts/install-server.sh | sudo bash
```
> 💡 **100% Plug & Play (Like VirtualHere!):** Installs `usbipd`, creates a `systemd` service, and adds a **`udev` hotplug rule** that **automatically exports any USB device as soon as you plug it into your Raspberry Pi/Linux server** — zero manual `usbip bind` commands required!

### 🐳 Docker / HomeLab / NAS Server (`docker-compose.yml`)
Run a Plug & Play USB/IP server on **Unraid, TrueNAS, Synology, or Proxmox** using [`docker/docker-compose.yml`](docker/docker-compose.yml):
```bash
git clone https://github.com/oscarlosu-lab/USBAnywhere.git && cd USBAnywhere/docker && docker compose up -d
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

## 📁 Structure

```text
USBAnywhere/
├── USBAnywhere.exe     # Main application (.NET 9 WPF)
├── usbipw.exe          # USB/IP client binary
├── devnode.exe         # PnP UDE device node manager
├── libusbip.dll        # USB/IP core library
├── drivers/            # Microsoft WHQL-certified x64 drivers
├── usb.ids             # USB vendor/product database (25,000+ entries)
├── CleanDrivers.ps1    # Driver cleanup utility
└── icon.ico            # Application icon
```

---

## 📄 License

> **USBAnywhere is free for personal, educational, and gaming use. Commercial redistribution, rebranding, or selling of this software/derivatives is strictly prohibited without explicit written consent from the author.**

See [LICENSE](LICENSE) for full legal terms and conditions.

## 🙏 Credits

| Project | Author | Contribution |
|---------|--------|--------------|
| [usbip-win2](https://github.com/vadimgrn/usbip-win2) | **Vadim Grn** | Microsoft-signed USB/IP drivers (WHQL certified). Core of the Windows client. |
| [OctoWrt](https://github.com/ihrapsa/OctoWrt) | **ihrapsa** | Original OpenWrt guide for CrealityBox. Inspiration for embedded device support. |
| [OctoWrt Fork](https://github.com/shivajiva101/OctoWrt) | **ShivaJiva** | Active OctoWrt maintenance. Updated releases for CrealityBox. |
| [USB/IP](https://www.kernel.org/doc/html/latest/usb/usbip_protocol.html) | **Linux Kernel** | Original USB/IP protocol |
| **USBAnywhere** | **SnakeFoxu (OscarLosu)** | .NET 9 WPF GUI, 1-Click SSH setup, VPN integration, latency modes & documentation |
