# 📖 USBAnywhere v3.0.1 User Manual

🌐 **Language / Idioma:** **English** | [Español](USAGE_ES.md)

## Main Interface

```
╔═════════════════════════════════════════════════════════════════════════╗
║ 🌐 USBAnywhere v3.0 — Virtual USB Hub      [🌙] [ENG] [⬆️ Update] 🟡🟢🔴║
╠═════════════════════════════════════════════════════════════════════════╣
║  Server:   ┌──────────────┐ ┌──────────┐┌────────┐┌──────┐┌──────────┐  ║
║            │192.168.1.50  │ │🔍 Scan   ││📋 List ││🔗 SSH││ ● VPN    │  ║
║            └──────────────┘ └──────────┘└────────┘└──────┘└──────────┘  ║
╠═════════════════════════════════════════════════════════════════════════╣
║  🖥️ USB Hubs (3)                                                        ║
║   ├─ 🖧 192.168.1.50 (2) [2ms]                                          ║
║   │   ├─ 🎮 1-1.2: Sony Corp. : DualSense Wireless Controller (054c:0ce6)║
║   │   └─ 🏎️ 1-1.4: Logitech, Inc. : G29 Driving Force Wheel (046d:c24f) ║
║   └─ 🖧 192.168.1.105 (1) [4ms]                                         ║
║       └─ 💾 2-1: SanDisk Corp. : Ultra Flair 64GB USB 3.0 (0781:5591)   ║
║                                                                         ║
║  ☑️ Connected Devices (1)                                               ║
║   └─ 🔌 Port 00: 1-1.2: DualSense Wireless Controller ← 192.168.1.50    ║
║                                                                         ║
║  ⭐ Favorites (2)                                                       ║
║   ├─ 🎮 1-1.2: DualSense Controller @ 192.168.1.50                      ║
║   └─ 🏎️ 1-1.4: G29 Driving Force @ 192.168.1.50                         ║
╠═════════════════════════════════════════════════════════════════════════╣
║ 📋 Activity Log                                               [Clear]   ║
╠─────────────────────────────────────────────────────────────────────────╣
║ [14:32:15] ☑ Connected: 1-1.2 from 192.168.1.50                         ║
║ [14:32:10] ℹ Scanning network...                                        ║
║ [14:32:12] ☑ 2 server(s) found                                          ║
╠═════════════════════════════════════════════════════════════════════════╣
║ ✓ Ready  [⚡ Mode: Zero-Copy]   Driver: Installed ✓ [Install][Uninstall] ║
╚═════════════════════════════════════════════════════════════════════════╝
```

### Title Bar (macOS style)
```
┌────────────────────────────────────────────────────────────────────────┐
│ 🌐 USBAnywhere v3.0 — Virtual USB Hub      [🌙] [ENG] [⬆️ Update] 🟡🟢🔴│
└────────────────────────────────────────────────────────────────────────┘
                                               │    │        │       │ │ │
                            Toggle Theme ──────┘    │        │       │ │ │
                            (Dark / Light)          │        │       │ │ │
                                     Language ──────┘        │       │ │ │
                                              Check Update ──┘       │ │ │
                                                       Minimize ─────┘ │ │
                                                       Maximize ───────┘ │
                                                       Close ────────────┘
```

### Server Panel
```
┌──────────────────────────────────────────────────────────────────────┐
│ Server:   [__192.168.1.50___] [🔍 Scan   ][📋 List][🔗 SSH][ ● VPN ] │
└──────────────────────────────────────────────────────────────────────┘
                │                    │          │        │       │
  Server IP     │    Search network ─┘          │        │       │
  (editable)    │    (scans subnet + [ms])      │        │       │
                │                               │        │       │
                │         List devices ─────────┘        │       │
                │         from current server            │       │
                │                                        │       │
                │              1-Click SSH Auto-Setup ───┘       │
                │              for Raspberry Pi / Linux          │
                │                                                │
                │                     Internet Connection ───────┘
                │                     (Tailscale/ZeroTier/WireGuard)
```

## Main Actions

### 🔍 Scan
Searches for USB/IP servers on your local network (port `3240`) and measures real-time TCP latency (`[2ms]`).

### 📋 List
Lists available USB devices across all discovered servers and the IP address entered in the text box.

### 🔗 SSH (& ⚡ 1-Click Auto-Setup)
Opens the SSH wizard for Raspberry Pi / Linux. Includes the **`⚡ 1-Click Auto-Setup`** button which automatically installs `usbipd`, enables persistent kernel modules, and configures `udev` hotplug auto-export.

### ● VPN
Scans your Virtual Private Networks (**Tailscale**, **ZeroTier**, and **WireGuard**), probes which peers have port `3240` active, and imports selected peers into the main server tree.
See [VPN_INTERNET_EN.md](VPN_INTERNET_EN.md) for complete configuration.

---

## 🌐 Connecting to Remote Server Platforms

USBAnywhere client connects transparently to any USB/IP host server:

### 🍎 Connecting to a macOS Server
1. On your Mac (Apple Silicon or Intel), run the 1-click installer:
   `ash
   curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere-Server/main/scripts/install-server-mac.sh | sudo bash
   `
2. In USBAnywhere Client, enter the Mac's IP (e.g. 192.168.1.50) and click **📋 List**.
3. Select your joystick, SimRacing wheel, or controller and click **▶️ Connect**.
4. See [macOS Server Manual](https://github.com/oscarlosu-lab/USBAnywhere-Server/blob/main/docs/USAGE_MACOS_EN.md) for details.

### 🖥️ Connecting to a Windows Desktop Server
1. Run USBAnywhere-Server.exe on the host PC and click **📤 Share** on desired devices.
2. In USBAnywhere Client, click **🔍 Scan** to auto-discover it, or type its IP and click **📋 List**.
3. Select the device and click **▶️ Connect**.

### 🐧 Connecting to a Linux / Raspberry Pi Server
1. Use the **⚡ 1-Click Auto-Setup** button in the SSH wizard, or run install-server.sh on the host.
2. The server will appear automatically in the device tree with real-time latency measurement.

### 📱 Connecting to an Android Host Server
1. Open USBAnywhere Android app on your Android device / Android TV.
2. Enter the Android device IP in the client and click **📋 List** to attach controllers or DACs.

---

## Context Menu (Right Click)

```
┌───────────────────────────────────────────────┐
│ 🎮 1-1.2: DualSense Wireless Controller       │ ← Right-click here
└───────────────────────────────────────────────┘
                        │
                        ▼
            ┌─────────────────────────┐
            │ ▶️ Connect              │
            │ ⏹️ Disconnect           │
            ├─────────────────────────┤
            │ ⭐ Add to Favorites     │
            │ ❌ Remove from Favorites│
            │ 🗑️ Remove server        │
            ├─────────────────────────┤
            │ 📋 Properties           │
            │ ✏️ Rename               │
            └─────────────────────────┘
```

| Option | Description |
|--------|-------------|
| ▶️ Connect | Connects the remote USB device (or favorite) to your PC |
| ⏹️ Disconnect | Releases the connected USB device |
| ⭐ Add to Favorites | Saves the device for quick reconnection |
| ❌ Remove from Favorites | Removes the device from the favorites list |
| 🗑️ Remove server | Removes the server from the tree |
| 📋 Properties | Displays detailed info (`VID:PID`, manufacturer, product, local port) |
| ✏️ Rename | Assigns a persistent custom friendly name to the device (`VID:PID`) |

## Shortcuts

- **Double-click** on device (in `USB Hubs` or `Favorites`) → Connect
- **Double-click** on connected device → Disconnect
- **Enter** in IP field → List devices

## Favorites & Performance Modes

Favorites are saved atomically in `config.json` and automatically reconnect (up to 3 attempts) if the network link drops briefly.

### ⚡ USB/IP Performance Modes (Status Bar Toggle)

You can switch the kernel driver receive mode at any time by clicking the **`[⚡ Mode: Zero-Copy]`** / **`[🏎️ Mode: Low-Latency]`** button in the bottom status bar:

| Mode | Best for | Technical Description |
|---|---|---|
| **⚡ Mode: Zero-Copy** *(Default)* | Storage disks, flash drives, HD webcams, SDR | Utilizes direct network buffers without intermediate kernel copies (`--receive-mode=zero-copy`), delivering maximum throughput. |
| **🏎️ Mode: Low-Latency** | Game controllers, SimRacing wheels, mice, keyboards, HID | Powered by Winsock Kernel (WSK) event callbacks (`--receive-mode=low-latency`) to minimize input lag on high-polling interactive peripherals. |

### 🔌 Clean Graceful Detach

When closing the application, shutting down Windows (`AutoDetachOnExit: true`), or uninstalling drivers, USBAnywhere gracefully detaches and releases all active USB ports before terminating. This prevents the remote Linux server from leaving ports in a locked (*busy*) state.

## 🖥️ System Tray

- When **minimizing** (or launching with `--minimized`), the app hides to the system tray.
- **Double-click** on the tray icon to restore the window.
- **Right-click** for quick menu: `📡 Scan Network`, `🔼 Show Window`, `❌ Exit`.

## 🔧 Status Bar & Driver Lifecycle

```
╔══════════════════════════════════════════════════════════════════════════╗
║ ✓ Ready  [⚡ Mode: Zero-Copy]   Driver: Installed ✓ [Install][Uninstall] ║
╚══════════════════════════════════════════════════════════════════════════╝
```

- **Install**: Cleans up any stale virtual nodes and installs the Microsoft WHQL-certified drivers (`usbip2_filter.inf` and `usbip2_ude.inf`), instantiating the `ROOT\USBIP_WIN2\UDE` virtual host controller.
- **Uninstall**: Detaches active USB/IP devices first, removes the virtual UDE node, and safely removes DriverStore packages in two stages without disrupting physical USB 3.0 Root Hubs or third-party virtual adapters (such as Parsec).

---

## 📚 More Information

- **[README.md](../README.md)** - Installation and main features
- **[RASPBERRY_PI_SERVER_EN.md](RASPBERRY_PI_SERVER_EN.md)** - Setup server on Raspberry Pi / Linux
- **[macOS Server Guide](https://github.com/oscarlosu-lab/USBAnywhere-Server/blob/main/docs/USAGE_MACOS_EN.md)** - Setup native server on Apple Mac (Apple Silicon & Intel)
- **[Windows ARM64 Guide](README_ARM64.md)** - Test-signing instructions for Snapdragon X Elite / Surface Pro
- **[VPN_INTERNET_EN.md](VPN_INTERNET_EN.md)** - Remote connection over Internet (Tailscale / ZeroTier / WireGuard)
- **[CHANGELOG.md](../CHANGELOG.md)** - Complete version history
