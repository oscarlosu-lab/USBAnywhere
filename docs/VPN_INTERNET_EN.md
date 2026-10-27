# 🌐 Internet Connection (VPN)

🌐 **Language / Idioma:** **English** | [Español](VPN_INTERNET_ES.md)

> **New in v1.7.0** - Connect remote USB devices over the Internet using Tailscale or ZeroTier.

## 📋 Summary

USBAnywhere now supports connecting to USB/IP servers **outside your local network** using free mesh VPNs. This means you can:

- Connect an office USB printer from home
- Access USB devices from a Raspberry Pi in another city
- Share license dongles between remote locations

## 🔧 Requirements

1. **Tailscale** (recommended) or **ZeroTier** installed on:
   - Your Windows PC (client)
   - The USB/IP server (Raspberry Pi, Linux, etc.)
2. Both devices joined to the **same VPN network**
3. USB/IP server running on the remote server

## 📦 Option 1: Tailscale (Recommended)

### Why Tailscale?
- ✅ Based on **WireGuard** (fast and secure)
- ✅ **100 devices free**
- ✅ Setup in 2 minutes
- ✅ Works behind NAT without port forwarding

### Installation on Windows

1. Download Tailscale from [tailscale.com/download](https://tailscale.com/download)
2. Install and login with Google, Microsoft, or GitHub
3. Done! Your PC now has a Tailscale IP (e.g., `100.x.x.x`)

### Installation on Raspberry Pi / Linux

```bash
# Install Tailscale
curl -fsSL https://tailscale.com/install.sh | sh

# Start and authenticate
sudo tailscale up

# Verify assigned IP
tailscale ip -4
```

### Configure USB/IP server on the Pi

```bash
# Install USB/IP
sudo apt update && sudo apt install -y linux-tools-generic hwdata

# Load module
sudo modprobe usbip_host

# Start daemon
sudo usbipd -D

# See available devices
usbip list -l

# Export a device (e.g., 1-1.4)
sudo usbip bind -b 1-1.4
```

## 📦 Option 2: ZeroTier

### Why ZeroTier?
- ✅ **25 devices free**
- ✅ Self-hosted available
- ✅ Good alternative to Tailscale

### Installation on Windows

1. Download from [zerotier.com/download](https://www.zerotier.com/download/)
2. Install and create an account
3. Create a network at [my.zerotier.com](https://my.zerotier.com)
4. Join your PC to the network with the Network ID

### Installation on Raspberry Pi / Linux

```bash
# Install ZeroTier
curl -s https://install.zerotier.com | sudo bash

# Join the network (replace NETWORK_ID)
sudo zerotier-cli join NETWORK_ID

# Verify status
sudo zerotier-cli status
```

## 🚀 Usage in USBAnywhere

1. **Open USBAnywhere**
2. **Click `🌐 VPN`** (teal button)
3. The app will automatically detect:
   - If you have Tailscale installed
   - If you have ZeroTier installed
4. **Scan VPN peers** looking for active USB/IP servers
5. **Select a peer** from the list
6. **Click Connect**
7. The peer's IP will be copied to the server field
8. Use **🔄 List** to see available devices

## 🔍 How it Works

```
┌─────────────────┐         ┌─────────────────┐
│   Your PC       │         │  Raspberry Pi   │
│   Windows       │         │  Linux Server   │
├─────────────────┤         ├─────────────────┤
│ USBAnywhere      │◄───────►│ usbipd          │
│ Tailscale       │   VPN   │ Tailscale       │
│ 100.64.0.1      │ tunnel  │ 100.64.0.2      │
└─────────────────┘         └─────────────────┘
        │                           │
        │     Internet/NAT          │
        └───────────────────────────┘
```

1. **Tailscale/ZeroTier** creates a VPN tunnel between both devices
2. **USBAnywhere** detects VPN peers
3. **Scans port 3240** on each peer to find USB/IP servers
4. **Connects** using the standard USB/IP protocol over the VPN

## ❓ Troubleshooting

### Tailscale/ZeroTier not detected
- Verify it's installed and running
- Check that you're authenticated (`tailscale status`)

### No peers appearing
- Make sure both devices are on the same VPN network
- Verify the USB/IP server is running (`usbipd`)
- Check firewall isn't blocking port 3240

### Slow connection
- Normal if there's significant geographic distance
- Tailscale uses "DERP" relays if it can't hole punch
- For better performance, ensure both devices have good connectivity

### "USB/IP: ❌" in peer list
- The USB/IP server isn't running on that peer
- Run `sudo usbipd -D` on the server

## 📊 Cost Comparison

| Solution | Price | Devices | Internet |
|----------|-------|---------|----------|
| **USBAnywhere + Tailscale** | **FREE** | 100 | ✅ |
| **USBAnywhere + ZeroTier** | **FREE** | 25 | ✅ |
| VirtualHere | $49 USD | Unlimited | ✅ |
| FlexiHub | $14/month | Per device | ✅ |
| USB Network Gate | $159 USD | Per server | ✅ |

## 🔗 Useful Links

- [Tailscale - Download](https://tailscale.com/download)
- [ZeroTier - Download](https://www.zerotier.com/download/)
- [Configure Raspberry Pi Server](RASPBERRY_PI_SERVER_EN.md)
- [User Manual](USAGE_EN.md)
