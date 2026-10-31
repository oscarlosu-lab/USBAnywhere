# 🍓 USB/IP Server on Raspberry Pi

🌐 **Language / Idioma:** **English** | [Español](RASPBERRY_PI_SERVER_ES.md)

Step-by-step guide to configure a USB/IP server on Raspberry Pi OS.

## 📋 Requirements

- Raspberry Pi (any model with USB)
- Raspberry Pi OS (Debian-based)
- Local network connection
- USB device to share

## ⚡ Option A: 1-Line Plug & Play Installer (Recommended)

Run this single command on your Raspberry Pi to automatically install `usbip`, configure the `systemd` daemon, and enable **`udev` hotplug auto-export** (so any USB device you plug into the Pi is shared automatically like VirtualHere):

```bash
curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere-Server/main/scripts/install-server-linux.sh | sudo bash
```

---

## 🚀 Option B: Manual Installation

```bash
# 1. Update system
sudo apt update && sudo apt upgrade -y

# 2. Install USB/IP
sudo apt install -y linux-tools-generic

# 3. Load kernel module
sudo modprobe usbip_host

# 4. Start daemon
sudo usbipd -D
```

## 📱 Share a USB Device

### Step 1: View available devices
```bash
sudo usbip list -l
```

Example output:
```
 - busid 1-1.4 (0bda:8152)
   Realtek Semiconductor Corp. : RTL8152 Fast Ethernet Adapter
```

### Step 2: Share device (bind)
```bash
sudo usbip bind -b 1-1.4
```

### Step 3: Verify it's shared
```bash
sudo usbip list -l
```
Should show `(attached)` next to the device.

## 🔄 Make Persistent (Autostart)

### Option 1: 1-Click Auto-Installer (Recommended)
If you used the 1-Click Installer (`install-server.sh`), `usbanywhere.service` and `usbanywhere-autobind.service` are **already installed and enabled automatically**.

### Option 2: Create manual systemd service

```bash
sudo nano /etc/systemd/system/usbipd.service
```

Content:
```ini
[Unit]
Description=USB/IP Daemon
After=network.target

[Service]
Type=forking
ExecStart=/usr/sbin/usbipd -D
ExecStartPost=/bin/sleep 1
ExecStartPost=/usr/sbin/usbip bind -b 1-1.4
Restart=on-failure

[Install]
WantedBy=multi-user.target
```

Enable:
```bash
sudo systemctl daemon-reload
sudo systemctl enable usbipd
sudo systemctl start usbipd
```

### Option 2: Add to rc.local

```bash
sudo nano /etc/rc.local
```

Add before `exit 0`:
```bash
modprobe usbip_host
usbipd -D
sleep 2
usbip bind -b 1-1.4
```

## ⚠️ Troubleshooting

### "usbip: command not found"
```bash
sudo apt install linux-tools-$(uname -r)
# Or if that doesn't work:
sudo apt install linux-tools-generic
```

### "Cannot find device"
```bash
# See all connected USB
lsusb
# See details
sudo usbip list -l
```

### "Device busy"
```bash
# Unmount if mounted
sudo umount /dev/sdX
# Then bind
sudo usbip bind -b 1-1.4
```

### "Connection refused" from Windows
```bash
# Verify daemon is running (usbanywhere if 1-click installer, or usbipd if manual)
sudo systemctl status usbanywhere 2>/dev/null || sudo systemctl status usbipd
# Or verify process
ps aux | grep -E 'usbipd|usbanywhere'
```

## 🔌 Unshare Device

```bash
sudo usbip unbind -b 1-1.4
```

## 📊 View Status

```bash
# Shared devices
sudo usbip list -l

# Active connections
sudo usbip port

# Logs (use usbanywhere for 1-click installer, or usbipd for manual service)
journalctl -u usbanywhere -u usbipd -f
```

## 🔥 Firewall

If you have an active firewall (ufw), restrict port 3240 to your local subnet (recommended):
```bash
# Allow only from your local network (e.g., 192.168.1.0/24)
sudo ufw allow from 192.168.1.0/24 to any port 3240 proto tcp comment 'USBAnywhere LAN'

# Or open port 3240 globally if on an isolated/dedicated LAN:
# sudo ufw allow 3240/tcp
```

---

## 📝 Notes

- The `bus-id` (e.g., `1-1.4`) may change if you reconnect the device
- Some USB devices are not compatible with USB/IP
- The server must be on the same network as the Windows client
