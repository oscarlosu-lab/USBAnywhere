# 🐳 USBAnywhere Server (Docker Container)

🌐 **Language / Idioma:** **English** | [Español](README_ES.md)

Containerized USB/IP server based on Alpine Linux / Debian, designed to export physical host USB devices over the network with minimal overhead.

[![License](https://img.shields.io/badge/License-GPLv3-blue.svg?style=flat-square)](https://www.gnu.org/licenses/gpl-3.0)
[![Docker](https://img.shields.io/badge/Docker-Container-blue?style=flat-square&logo=docker)](https://www.docker.com/)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20Project-FF5E5B?style=flat-square&logo=ko-fi&logoColor=white)](https://ko-fi.com/oscarlosu)

---

## 🚀 Quick Start

### 1. Using Docker Compose (Recommended)
```bash
cd docker
docker compose up -d
```

### 2. Using Standalone Docker Run
```bash
docker run -d \
  --name usbanywhere-server \
  --restart unless-stopped \
  --privileged \
  --net=host \
  -v /dev/bus/usb:/dev/bus/usb \
  -v /lib/modules:/lib/modules:ro \
  -e AUTO_BIND_ALL=true \
  usbanywhere-server:latest
```

---

## ⚙️ Configuration & Environment Variables

| Variable | Default | Description |
|---|---|---|
| `AUTO_BIND_ALL` | `true` | Automatically export all attached physical USB peripherals (excluding root hubs) |
| `BIND_BUSIDS` | `""` | Comma-separated list of specific bus IDs to export (e.g. `1-1.2,1-1.4`) |
| `TCP_PORT` | `3240` | USB/IP network listening port |

---

## 🛡️ Host Requirements

Because USB/IP requires direct interaction with the host kernel's USB bus, the container requires:
1. `--privileged` flag or `CAP_SYS_ADMIN` capability.
2. Direct bind mount of host USB devices: `-v /dev/bus/usb:/dev/bus/usb`.
3. Host kernel modules loaded:
   ```bash
   sudo modprobe usbip_core
   sudo modprobe usbip_host
   ```

---

## 📄 License

GPL-3.0 License. Developed by [oscarlosu-lab](https://github.com/oscarlosu-lab).
