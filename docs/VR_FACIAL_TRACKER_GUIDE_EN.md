# 🥽 Optimization Guide: VR, HTC Vive Facial Tracker & High-Bandwidth Devices

🌐 **Language / Idioma:** [Español](VR_FACIAL_TRACKER_GUIDE_ES.md) | **English**

---

## 🎯 Introduction & Use Case

Real-time facial tracking in Virtual Reality (**VR Facial Tracking**) for platforms such as **VRChat** and **Resonite** using tools like **VRCFaceTracking (VRCFT)** requires streaming steady, high-bandwidth video streams from IR cameras mounted on your headset back to your gaming PC.

The gold standard hardware for this workflow is the **HTC Vive Facial Tracker** (Hardware ID `0bb4:0320`), which houses dual infrared cameras capturing uncompressed frames at 60 FPS.

```
┌─────────────────────────────────┐                 ┌─────────────────────────────────┐
│        VR HEADSET               │   Wi-Fi 5/6GHz  │            GAMING PC            │
│  HTC Vive Facial Tracker (60Hz) │ ──────────────> │   USBAnywhere Client [Zero-Copy]│
│   USBAnywhere Android/Linux Srv │                 │   VRCFaceTracking + SRAnipal    │
└─────────────────────────────────┘                 └─────────────────────────────────┘
```

### ⚠️ The VirtualHere Bottleneck
Traditional USB-over-IP utilities such as *VirtualHere* suffer from notable issues with high-bandwidth devices:
* The raw dual-camera video stream overwhelms standard user-space buffer copies over network sockets.
* Nagle's algorithm and Wi-Fi packet jitter lead to **frame freezing**, tracker disconnects, and progressive latency drift in avatar mouth blendshapes.
* VirtualHere restricts its free edition to a single device and charges a $49 per-server commercial license.

### ⚡ The USBAnywhere Solution
1. **Zero-Copy Mode (`⚡ Mode: Zero-Copy`)**: URB packet buffers are routed directly between the network subsystem and the Windows virtual USB bus driver inside the kernel, eliminating user-space memory copies and reducing CPU processing overhead to under 1 ms.
2. **Low-Latency Winsock Kernel (WSK) Pipeline**: Enforces immediate packet transmission (`TCP_NODELAY`), forwarding camera frames without latency spikes.
3. **Headless & Minimized Startup**: Native **Start minimized to tray** toggle allows the client to launch silently on PC startup and reconnect favorite devices without user intervention.
4. **No Device Limits**: Run your Vive Facial Tracker, Bluetooth dongle for Valve Index controllers, and auxiliary sensors at the same time at zero cost.

---

## 🛠️ Supported Hardware Topologies

### Option A: Standalone VR Headset (Steam Frame / Quest 2/3/Pro / Vive XR Elite / Focus 3)
* **Server**: The headset runs the **USBAnywhere Android** app in background mode (rootless using Android's `UsbManager`).
* **Hardware**: The HTC Vive Facial Tracker is attached to the headset's USB-C port (or through an active power-delivery OTG splitter).
* **Client**: Your gaming PC runs **USBAnywhere Client** for Windows.

### Option B: Companion Raspberry Pi / Mini-PC in Pocket or Headstrap
* **Server**: Raspberry Pi Zero 2W, Pi 4, or Pi 5 running `install-server-linux.sh`.
* **Hardware**: Powered by an external 5V 2.4A/3A USB battery bank in your pocket or mounted to your headset counterweight, streaming over Wi-Fi.

---

## 📶 Wi-Fi & Network Calibration for Zero Jitter

Streaming a continuous 60 FPS uncompressed camera feed without micro-stutters requires optimal network parameters:

| Parameter | Recommended Setting | Purpose |
| :--- | :--- | :--- |
| **Wi-Fi Band** | **5 GHz (Wi-Fi 5/6) or 6 GHz (Wi-Fi 6E/7)** | 2.4 GHz has inadequate bandwidth and severe channel congestion. |
| **Channel Width** | **80 MHz or 160 MHz** | Delivers maximum continuous throughput for camera packet streams. |
| **SSID Isolation** | **Dedicated VR SSID** | Prevents headset from roaming or dropping to 2.4 GHz. |
| **DFS Channels** | **Disabled (use standard channels 36-48 or 149-165)** | DFS channels dynamically hop if radar signals are detected, terminating your stream. |
| **PC Host Connection**| **Gigabit / 2.5G Ethernet Cable** | The gaming PC **must** be wired; never stream Wi-Fi to Wi-Fi. |
| **QoS / WMM** | **Enabled on router** | Prioritizes real-time interactive multimedia packets. |

---

## 🚀 Step-by-Step Setup Guide

### Step 1: Physical Connection & Power Delivery
1. Connect the HTC Vive Facial Tracker to the USB-C port of your headset or companion Raspberry Pi.
2. ⚠️ **Power Delivery Warning**: The Vive Facial Tracker requires at least 500mA at 5V. If the USB port suffers from voltage drops (common on low-power OTG cables or nearly depleted headset batteries), the device will reset and report as `HTC Boot`. Use high-quality OTG adapters with active power pass-through.

### Step 2: Configure USBAnywhere on your Gaming PC
1. Launch **USBAnywhere** on your Windows PC.
2. In the bottom status bar, ensure the mode button is set to **`⚡ Mode: Zero-Copy`** (green button). If it says Low-Latency (orange), click it to switch to Zero-Copy.
3. Check the option in the bottom-left corner:
   - ☑️ **Start minimized to tray**
4. Click **🔍 Scan** or enter your headset / Pi server IP and click **📋 List**.
5. Locate the device named `HTC Multimedia Camera` or ID `0bb4:0320`.
6. Right-click the device and select:
   - **⭐ Add to Favorites**
7. Under the **⭐ Favorites** tree node, ensure auto-connect is enabled.
8. Double-click the device or click **🔗 Connect**.

### Step 3: Verify in Windows Device Manager
1. Open Windows **Device Manager** (`devmgmt.msc`).
2. Expand the **Cameras** or **Imaging devices** category.
3. Confirm that **`HTC Multimedia Camera`** is listed cleanly without any yellow exclamation icons.

### Step 4: Configure VRCFaceTracking (VRCFT) & VRChat
1. Start **SRAnipal Runtime** (part of VIVE Console for SteamVR).
2. Launch **VRCFaceTracking** (v5.0 or newer).
3. In the **Module Registry** tab, search for and install:
   - `SRAnipal Tracking Module` (by the VRCFT community).
4. The status indicator in VRCFaceTracking will turn green (**Active**).
5. Launch **SteamVR** and **VRChat**: your facial expressions, lip sync, and jaw movements will map directly to compatible avatars.

---

## 🔧 Troubleshooting Common Issues

### 1. Device is detected as `HTC Boot` instead of a Camera
* **Cause**: Device entered firmware bootloader mode due to USB power interruption.
* **Fix**:
  - Unplug and re-plug the tracker.
  - Do not unplug if Windows is running an initial firmware update.
  - Use a powered OTG splitter if the headset battery is delivering inconsistent voltage.

### 2. Intermittent micro-stutter or periodic disconnects
* **Cause**: Router channel switching or band steering (2.4 GHz vs 5 GHz).
* **Fix**:
  - Lock your router to a fixed non-DFS 5 GHz channel (channel 36 or 40).
  - Ensure USBAnywhere remains in **Zero-Copy** mode.

### 3. Blendshapes do not move in VRChat despite successful connection
* **Cause**: Poor ambient lighting or uninitialized SRAnipal service.
* **Fix**:
  - The IR cameras require standard ambient illumination to detect facial contour depth.
  - Restart VRCFaceTracking with administrator rights after USBAnywhere attaches the device.

---

## 🌟 Comparison Overview for the VR Community

| Feature | VirtualHere (Free/Paid) | USBAnywhere v3.0+ |
| :--- | :---: | :---: |
| **Licensing** | Proprietary ($49/server) | **100% Free & Open Source (GPLv3)** |
| **Device Limit** | 1 device on free tier | **Unlimited simultaneous devices** |
| **Zero-Copy Kernel Pipeline** | ❌ Not available | **✅ Native (WHQL-certified drivers)** |
| **Low-Latency WSK Sockets** | ⚠️ Partial | **✅ Optimized for 60 FPS VR video** |
| **Start Minimized to Tray** | ⚠️ Requires custom CLI scripts | **✅ 1-Click UI & System Tray setting** |
| **Seamless Background Auto-Connect** | ⚠️ Inconsistent after reboot | **✅ Resilient persistent favorites engine** |
