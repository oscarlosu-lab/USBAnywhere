# ⚡ Scripts de Automatización y Despliegue de USBAnywhere

🌐 **Idioma / Language:** [English](README.md) | **Español**

Este directorio contiene scripts de aprovisionamiento, suites de control y herramientas de automatización multimedia para el ecosistema USBAnywhere.

[![Licencia](https://img.shields.io/badge/Licencia-GPLv3-blue.svg?style=flat-square)](https://www.gnu.org/licenses/gpl-3.0)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Apoyar%20Proyecto-FF5E5B?style=flat-square&logo=ko-fi&logoColor=white)](https://ko-fi.com/oscarlosu)

---

## 📜 Índice de Scripts

### 1. `usbanywhere-ctl` — Suite de Control Unificada (macOS y Linux)
Panel interactivo con interfaz TUI para terminal y herramienta de gestión CLI sin dependencias externas.
- **Panel de Control Interactivo (TUI)**: `usbanywhere-ctl` (o `usbanywhere-ctl dashboard`)
- **Doctor del Sistema**: `usbanywhere-ctl doctor`
- **Autoprueba del Protocolo**: `usbanywhere-ctl test`
- **Gestión del Servicio**: `sudo usbanywhere-ctl start|stop|restart|status`
- **Gestión de Dispositivos**: `usbanywhere-ctl list`, `sudo usbanywhere-ctl share <busid>`

### 2. `install-server-mac.sh` — Instalador 1-Click para macOS
Instalador automático para macOS (Apple Silicon M1-M4 e Intel x86_64).
- Instala el demonio nativo en Swift y el servicio LaunchDaemon.
- Instala `usbanywhere-ctl` en `/usr/local/bin/`.

```bash
curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere-Server/main/scripts/install-server-mac.sh | sudo bash
```

### 3. `install-server-linux.sh` / `install-server.sh` — Instalador 1-Click para Linux y Raspberry Pi
Instalador automático para distribuciones Linux y Raspberry Pi OS (Debian/Ubuntu/Raspbian/Arch/Fedora).
- Carga módulos del núcleo `usbip_core` y `usbip_host`.
- Instala paquete de espacio de usuario `usbip` y servicio systemd.
- Configura regla udev para auto-exportar en caliente.
- Instala `usbanywhere-ctl` en `/usr/local/bin/`.

```bash
curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere-Server/main/scripts/install-server-linux.sh | sudo bash
```

---

## 📄 Licencia

Licencia GPL-3.0. Desarrollado por [oscarlosu-lab](https://github.com/oscarlosu-lab).
