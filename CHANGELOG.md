# CHANGELOG - USBAnywhere

## [3.0.1] - 2026-10-26

### Security & Hardening (Zero-Trust Audit)
- **Binary & DLL Integrity Gate (Findings C-01, F-04, A-02)**:
  - Synchronized and cryptographically verified SHA-256 hashes in `UsbipService` and `UsbipBinaryIntegrityTests` for `usbipw.exe`, `devnode.exe`, companion DLLs (`libusbip.dll`, `resources.dll`), and drivers across x64 and ARM64.
  - Implemented fail-closed execution gate blocking tampered or unverified native binaries and companion DLLs before launching sub-processes.
  - Mitigated binary planting vectors (F-01, F-05) by binding `pnputil.exe`, `cmd.exe`, and `wt.exe` strictly to absolute System32 and WindowsApps paths.
- **Linux & Container Security Hardening (Findings C-01, A-03, M-01, M-03)**:
  - Hardened `scripts/install-server.sh` with strict bash modes (`set -euo pipefail`), `curl -fsSL`, and a safe auto-export policy excluding local HID (Class 03: keyboards/mice) and Mass Storage (Class 08: USB disks) by default via `/etc/default/usbanywhere`.
  - Added root `.dockerignore` excluding 1.9 GB build caches, Git history, and `.env*` secrets from Docker daemon context.
  - Fixed Dockerfile context to copy `scripts/install-server.sh` and added `docker/entrypoint.sh` enforcing the safe class policy in containers.
- **Server Windows Hardening (Findings SW-01, SW-02)**:
  - Restricted Windows Firewall rules for TCP port 3240 to `profile=private,domain` and `remoteip=localsubnet` or explicit subnet whitelists.
  - Removed unused PIN configuration placeholder and bumped `Microsoft.NET.Test.Sdk` 17.12.0 for .NET 9 alignment.

### Fixed & Improved
- **Process & Lifecycle Robustness (Findings F-13, F-14, H-11, C-03)**:
  - Added recursive process tree termination (`Kill(entireProcessTree: true)`) on timeouts to eliminate orphaned USB/IP child handles.
  - Implemented non-blocking asynchronous driver status queries in `DriverService` eliminating UI startup freezes.
  - Exact `(ServerIP, BusId)` matching during device disconnect to prevent ambiguous port detachments across identical IP subnets.
  - Asynchronous graceful shutdown hooked into `OnClosing` and update flows, ensuring all active remote ports are cleanly detached.
- **UX & Observability Enhancements (Findings F-11, H-06, H-07, H-14)**:
  - Added Enter key shortcut on Server IP input field to trigger immediate server device enumeration.
  - Integrated `FileLoggerProvider` connecting standard `ILogger` to rolling daily disk logs in `%LOCALAPPDATA%\USBAnywhere\logs`.
  - Global unhandled exception handlers catching UI dispatcher, AppDomain, and unobserved task exceptions.
  - Explicit error reporting when checking for updates instead of ambiguous "You have the latest version".
- **Documentation & Packaging Parity (Findings F-18, B-06, M-09, M-10, M-11, C-02)**:
  - Added `README_ARM64.md` documenting Test Signing (`bcdedit /set testsigning on`) requirements on Windows ARM64.
  - Fixed 404 links and unified service names across English and Spanish documentation.
  - Updated Scoop and WinGet manifests with v3.0.1 release URLs and SHA-256 hashes.
  - Unified solution-wide versioning via `Directory.Build.props` (v3.0.1).

## [3.0.0] - 2026-10-24

### Added
- **Major Brand Evolution: USBAnywhere v3.0.0** 🌐
  - Rebranded from `SnakeUSBIP` to **`USBAnywhere`** — the free, open-source alternative to VirtualHere, VirtualUSB, and USB Network Gate.
  - Updated executable (`USBAnywhere.exe`), installer (`USBAnywhere_Setup_v3.0.0.exe`), portable packages (`x64` & `arm64`), UI header, system tray, and logs (`usbanywhere_YYYYMMDD.log`).
  - Automatic configuration migration from `%AppData%\SnakeUSBIP\config.json` to `%AppData%\USBAnywhere\config.json` on first startup.
- **1-Click Linux & Raspberry Pi Plug & Play Server Auto-Setup** ⚡
  - Added `⚡ 1-Click Auto-Setup` inside the SSH dialog and `scripts/install-server.sh` (`curl -sSL ... | sudo bash`) to automatically install `usbipd`, enable `usbip_core`/`usbip_host` kernel modules across reboots, configure `usbanywhere.service` (`systemd`), and install a `udev` hotplug rule (`99-usbanywhere-autobind.rules`) that auto-exports any plugged-in USB device.
- **Interactive Receive Mode Selector in Status Bar** 🏎️
  - Added live status bar toggle button (`⚡ Mode: Zero-Copy` / `🏎️ Mode: Low-Latency`) to switch kernel WSK receive modes on the fly and persist the preference in `config.json`.
- **Live Server Latency Meter (`[ms]`) & Custom Device Renaming (`✏️`)** 📊
  - Displays real-time TCP latency in milliseconds next to every discovered server in the `USB Hubs` tree.
  - Added `✏️ Rename` in the device context menu to assign custom friendly names per `VID:PID`.
- **Moonlight / Sunshine CLI Automation & Docker Server Support** 🎮
  - Added CLI flags `--minimized --connect <IP> <BUSID>` for seamless game-streaming automation.
  - Added `docker/docker-compose.yml` for deploying a Plug & Play USB/IP server on HomeLab/NAS systems (Proxmox, Unraid, TrueNAS, Synology).
- **Live VPN Peer Discovery (`Tailscale`, `ZeroTier`, `WireGuard`)** 🛡️
  - `VpnDialog` now performs live peer discovery and asynchronous TCP port `3240` probing (`1000 ms` timeout), importing selected peers directly into the main server tree.
- **Full HD 1080p Bilingual Video Tutorials (YouTube Edition)** 🎬
  - Included native 1080p MP4 video tutorials (`docs/youtube_tutorial_es.mp4` & `docs/youtube_tutorial_en.mp4`) with real WPF UI captures, neural voiceover, and `.srt` subtitles.
- **Expanded Zero-Trust Unit Test Suite (67 Tests)** 🧪
  - Expanded xUnit test suite to **67 passing tests** covering `UsbIdsService` hex normalization, custom device names, `LocalizationService` ES/EN parity, `InputValidator`, and atomic `ConfigService` persistence.

### Fixed & Hardened
- **Hardened Inno Setup Installer & Uninstaller (`USBAnywhere.iss`)** 📦
  - Added `AppMutex=USBAnywhere_SingleInstance_Mutex`, `CloseApplications=yes`, `UninstallDisplayIcon`, `--minimized` Windows startup argument, pre-install UDE node cleanup to prevent duplicate host controllers, synchronous `waituntilterminated` UDE cleanup on uninstall, and `[UninstallDelete]` cleanup of runtime directories (`logs`, `temp`, `data`).
- **Safe PnP Driver Lifecycle (`DriverService` & `CleanDrivers.ps1`)** 🛡️
  - Automatic detachment of active USB/IP devices prior to driver uninstallation.
  - Safe two-stage PnP driver removal (`/uninstall` followed by DriverStore `/force` fallback) that preserves physical USB 3.0 Root Hubs (`USB\ROOT_HUB30`) and third-party virtual USB adapters (e.g., `Parsec Virtual USB Adapter`).
  - Parallel `stdout`/`stderr` reading with 60-second process timeout in `DriverService.RunProcessAsync`.
- **USB Vendor/Product Hex Normalization (`UsbIdsService`)** 🔍
  - Fixed bug where `TrimStart('0', 'x')` stripped leading zeros from 4-digit hex VID/PIDs (`046d` Logitech, `0bda` Realtek, `045e` Microsoft, `05ac` Apple), and added automatic loading of the bundled 25,000+ entry `usb.ids` database.
- **Cryptographic Auto-Updater (`UpdateService`)** 🔄
  - Fixed `MainWindow` passing `updateInfo.HashUrl` (`SHA256SUMS.txt`) into `StartUpdateAsync` so mandatory SHA-256 verification succeeds.
- **Multi-Device Disconnect & Auto-Reconnect (`MainWindow` & `ConnectionMonitorService`)** 🔌
  - Fixed `DisconnectDeviceCoreAsync` matching `(ServerIP, BusId)` before `ServerIP` when multiple devices are attached from the same server.
  - Fixed `ConnectionMonitorService` executing all 3 reconnect attempts and firing `ConnectionRestored`.
  - Fixed `App.xaml.cs` tracking `_ownsMutex` before releasing the single-instance mutex on exit, and converted theme brushes to `DynamicResource` for live Dark/Light switching.

## [2.0.4] - 2026-10-19

### Added
- **Receive Mode Selection** ⚡
  - Added support for `--receive-mode=zero-copy` (high-throughput storage/webcams) and `--receive-mode=low-latency` (fast WSK event callbacks for HID devices like mice/keyboards).
  - Configurable via `DefaultReceiveMode` and per-favorite device settings.
- **Graceful Detach on Exit** 🔌
  - Application automatically releases all connected remote USB ports upon closing or system shutdown (`AutoDetachOnExit`), preventing the Linux server bus from remaining in a locked/busy state.
- **UDE Virtual Node Creator Integration** 🛠️
  - Integrated `devnode.exe` with SHA-256 integrity validation (`5B9C...6806`) for instantiating the `ROOT\USBIP_WIN2\UDE` virtual host controller in Windows PnP.
- **Active Device Detection** 🔍
  - Real-time PnP device queries via `pnputil /enum-devices /class USB` verifying active controller state rather than just staged DriverStore packages.
- **Unit Test Suite** 🧪
  - Added unit test coverage for `ConfigService` (atomic saving, corruption recovery, settings persistence). 49/49 tests passing.

### Fixed
- **Visual Driver Status False Positive** 🐛
  - Fixed bug in `MainWindow.xaml.cs` where missing drivers returning `"FALTANTE"` evaluated to true and displayed "Installed ✓". Now strictly checks for `"INSTALADO"`.
- **Connection Monitor Timer Re-entrancy** ⏱️
  - Fixed race conditions in `ConnectionMonitorService` by disabling `AutoReset` and ensuring sequential ticks with thread synchronization.
- **Atomic Configuration Persistence** 💾
  - Replaced direct file write with atomic `.tmp` + `File.Move(..., overwrite: true)` and file-level lock, eliminating 0-byte config corruptions on sudden exit.
- **Legacy Driver Conflicts** 🛡️
  - Completely removed obsolete 2022 `usbip_vhci` driver files to prevent dual-controller conflicts and ensure 100% compatibility with Windows 11 Core Isolation / Memory Integrity.
- **Uninstaller Device Node Cleanup** 🧹
  - Inno Setup uninstaller now removes the `ROOT\USBIP_WIN2\UDE` device node cleanly on uninstallation.

## [2.0.3] - 2025-12-27

### Added
- **Single Instance** 🔒 - Mutex prevents multiple app instances from running
- **Full Translation** 🌐 - Activity Log messages now translate when switching language

### Fixed
- **Tray Icon Cleanup** 🧹 - Fixed ghost tray icon when closing application
- **OnClosing Handler** - Proper disposal of NotifyIcon and ConnectionMonitor

### Added - USBAnywhere Server (Windows) 🖥️
- **New Component:** USBAnywhere Server - GUI wrapper for usbipd-win
  - Auto-installation of usbipd-win driver (bundled MSI)
  - One-click Share/Stop buttons for USB devices
  - Admin manifest (single UAC prompt at startup)
  - WMI-enriched device names (shows "CruzerBlade" instead of generic)
  - Uninstall option to clean up drivers

### Improved
- **Client Device Names** 📛
  - Now uses `usb.ids` database for descriptive remote device names
  - Shows "SanDisk Corp. : CruzerBlade" instead of generic text

## [2.0.2] - 2025-12-26

### Fixed
- **Auto-Update** 🐛
  - Fixed a critical bug where the application failed to parse release tags from GitHub API due to case sensitivity.
  - Added proper JSON property mapping for reliability.

## [2.0.1] - 2025-12-26

### Improved
- **Notifications** 🔔
  - Implemented hybrid notification system:
    - **Visible Window:** Custom WPF Toast popup (non-intrusive, auto-hides).
    - **Minimized:** Standard Windows BalloonTip (ensures visibility).
  - Notifications no longer accumulate in Windows Action Center.
- **Device Persistence** 💾
  - Application now detects devices that were already connected before startup.
  - Connected devices are prominently displayed with full descriptions.
- **Connection Monitor** ⚡
  - Reduced monitoring interval from 10s to 2s for faster disconnect detection.
  - Improved device matching logic (OR -> AND condition) to avoid false positives.
- **UI UX** ✨
  - Connected devices node now shows full device name and ID (e.g., `1-1: Camera (046d:0825)`).

## [2.0.0] - 2025-12-25

### Added
- **Revamped Architecture (Migration)** 🏗️
  - Complete codebase migration from PowerShell to **.NET 9 (C# / WPF)**.
  - Delivers higher performance, stability, and an ultra-fast native GUI.
- **Rename USB Devices** ✏️
  - New "Rename" context menu in the device list.
  - Allows assigning friendly names (e.g., "Laser Camera") that are persistently saved.
  - Your custom names take precedence over automatic hardware detection.
- **Updated Hardware Database** 📚
  - Updated `usb.ids` to the December 2025 version.
  - Native recognition of +17,000 new devices.
- **Complete x64 Distribution** 📦
  - Available as both **Portable** (ZIP) and **Installer** (EXE).

### Fixed
- **Identification:** Fixed hardware ID conflict for `0bda:5100` (Acmer Camera vs Realtek Adapter).
- **Logging:** Cleanup of redundant activity log messages.
- **Installer:** Fixed installer compilation errors.
