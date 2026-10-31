# 🌐 USBAnywhere — La Alternativa Libre y Gratuita a VirtualHere para Windows

🌐 **Idioma / Language:** [English](README.md) | **Español**

**v3.1.0** | [🌍 Web Oficial](https://oscarlosu-lab.github.io/USBAnywhere/) | [📦 Releases y Descargas](https://github.com/oscarlosu-lab/USBAnywhere/releases/latest) | [🌐 USBAnywhere Server (Windows & macOS)](https://github.com/oscarlosu-lab/USBAnywhere-Server) | [📱 USBAnywhere Android](https://github.com/oscarlosu-lab/USBAnywhere-Android) | [💬 Comunidad Reddit](https://www.reddit.com/r/UsbAnywhere/) | [📖 Manual de Usuario](docs/USAGE_ES.md) | [🌐 Conexión VPN](docs/VPN_INTERNET_ES.md)

> **¿Por qué pagar 49,00 $ por servidor y estar limitado a un único dispositivo USB?**
> **USBAnywhere** conecta **ilimitados** periféricos USB remotos por LAN, Wi-Fi e Internet con **cero licencias**, **drivers certificados WHQL por Microsoft** y **latencia ultrabaja ajustable**.

Transforma cualquier dispositivo Linux (o PC Windows) en un Hub USB Virtual de alto rendimiento accesible desde Windows 10 y 11. Compatible con **Raspberry Pi, Orange Pi, Banana Pi, Proxmox/HomeLab, Steam Deck, routers OpenWRT, CrealityBox** y cualquier servidor ARM/x86 con Linux.

[![CI Build & Tests](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/ci.yml/badge.svg)](https://github.com/oscarlosu-lab/USBAnywhere/actions/workflows/ci.yml)
[![GitHub Downloads](https://img.shields.io/github/downloads/oscarlosu-lab/USBAnywhere-Downloads/total?style=flat-square&logo=github&color=blue)](https://github.com/oscarlosu-lab/USBAnywhere-Downloads/releases)
[![GitHub Stars](https://img.shields.io/github/stars/oscarlosu-lab/USBAnywhere?style=flat-square&logo=github&color=yellow)](https://github.com/oscarlosu-lab/USBAnywhere/stargazers)
[![Licencia](https://img.shields.io/badge/Licencia-GPLv3-blue.svg?style=flat-square)](https://www.gnu.org/licenses/gpl-3.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20(x64%20%26%20ARM64)-lightgrey?style=flat-square&logo=windows)](https://github.com/oscarlosu-lab/USBAnywhere)
[![Comunidad Reddit](https://img.shields.io/badge/Reddit-r%2FUsbAnywhere-FF4500?style=flat-square&logo=reddit&logoColor=white)](https://www.reddit.com/r/UsbAnywhere/)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Apoyar%20Proyecto-FF5E5B?style=flat-square&logo=ko-fi&logoColor=white)](https://ko-fi.com/oscarlosu)

---

## ⚔️ ¿Por qué cambiar de VirtualHere a USBAnywhere?

| Característica | **USBAnywhere v3.0** | **VirtualHere** | **USB Network Gate** |
| :--- | :---: | :---: | :---: |
| **Precio y Licencia** | **100% Gratis y Código Abierto (GPLv3)** | 49,00 USD / Servidor | +159,00 USD |
| **Dispositivos Simultáneos (Free)** | **♾️ Ilimitados** | ⚠️ Solo 1 dispositivo | ❌ Trial limitado |
| **Instalación Servidor en 1-Click** | ✅ **Auto-Instalador SSH y `udev` Hotplug** | ❌ Manual por consola | ❌ Demonio privativo |
| **Modos de Latencia de Red** | ✅ **Dual: Low-Latency WSK y Zero-Copy** | ⚙️ Genérico | ⚙️ Genérico |
| **Interfaz de Usuario** | ✅ **WPF Moderna (Temas Oscuro/Claro + Ping `[ms]`)** | 🕰️ Árbol clásico Win98 | 💻 Pesada |
| **Drivers (Windows 10/11)** | ✅ **Certificados WHQL (Soporte HVCI)** | ✅ Firmados | ✅ Firmados |
| **Integración VPN Nativa** | ✅ **Detección Tailscale, ZeroTier y WireGuard** | 💰 Extra / Manual | ❌ Manual |

---

## 🎯 Diseñado para las Comunidades cansadas de Muros de Pago

1. 🎮 **Cloud Gaming, Moonlight / Sunshine y Steam Deck**
   Conecta mandos Xbox/DualSense (con vibración completa, gatillos adaptativos y audio), dongles de auriculares inalámbricos y teclados desde la Raspberry Pi de tu salón directamente a tu PC Gaming.
2. 🏎️ **SimRacing y Simuladores de Vuelo (HOTAS / VR)**
   Conecta volante, pedalera, caja de cambios, botonera y trackers VR simultáneamente por Ethernet usando el modo **Low-Latency WSK** sin pagar licencias por superar 1 dispositivo.
3. 🖨️ **Impresión 3D (Klipper, Mainsail, OctoPrint, CrealityBox)**
   Comparte múltiples cámaras USB, acelerómetros ADXL345 y placas de impresoras 3D sin cables.
4. 🖥️ **HomeLab, Proxmox y Self-Hosted**
   Pasa coordinadores Zigbee/Z-Wave, llaves de seguridad física y dongles de licencia (AutoCAD, iLok, eToken) por LAN o VPN Tailscale.
5. 🥽 **Realidad Virtual y Seguimiento Facial (HTC Vive Facial Tracker, VRChat, Quest)**
   Transmite streams duales de cámaras infrarrojas a 60 FPS sin comprimir hacia VRCFaceTracking (VRCFT) usando el modo **Zero-Copy** y arranque minimizado en bandeja sin los congelamientos ni cortes habituales de VirtualHere. 👉 **[Leer la Guía de Optimización para VR y Facial Tracker](docs/VR_FACIAL_TRACKER_GUIDE_ES.md)**.

---

## 🎬 Vídeos Tutoriales y Guías (YouTube)

### 🌟 Masterclass Completa (La Alternativa GRATIS a VirtualHere)
<a href="https://www.youtube.com/watch?v=gKoUBd5_lj8">
  <img src="https://img.youtube.com/vi/gKoUBd5_lj8/maxresdefault.jpg" width="70%" alt="USBAnywhere: Tutorial Completo Alternativa a VirtualHere">
</a>

▶️ **[Ver Masterclass Completa en YouTube](https://www.youtube.com/watch?v=gKoUBd5_lj8)**

### 🔥 Tutorial Especializado: Láser Diodo y Cámara en LightBurn
Conecta tu grabadora láser diodo (GRBL/Ruida) y tu cámara cenital simultáneamente por Wi-Fi o VPN sin desconexiones ni límites de dispositivos:

<a href="https://www.youtube.com/watch?v=xz9jhgLx3pc">
  <img src="https://img.youtube.com/vi/xz9jhgLx3pc/maxresdefault.jpg" width="70%" alt="Conecta tu Láser Diodo y Cámara a LightBurn sin Cables">
</a>

▶️ **[Ver Tutorial de Láser y Cámara LightBurn en YouTube](https://www.youtube.com/watch?v=xz9jhgLx3pc)**

---

## 🆕 Novedades en v3.0.0 (Evolución a USBAnywhere)

- 🌐 **Nueva marca: USBAnywhere** - Evolución de `SnakeUSBIP` para posicionarse como la alternativa abierta número 1 frente a VirtualHere y VirtualUSB, con migración automática de tu configuración desde v2.x.
- ⚡ **Auto-Instalador SSH y Plug & Play en 1-Click** - Convierte cualquier Raspberry Pi, Debian o Ubuntu en un servidor USB/IP con auto-exportación `udev` hotplug automática al conectar dispositivos.
- 🏎️ **Selector Interactivo de Modos de Rendimiento (`⚡ Modo: Zero-Copy` / `🏎️ Modo: Low-Latency`)** - Alterna en vivo desde la barra de estado entre `zero-copy` (máxima velocidad para almacenamiento y cámaras) y `low-latency` (callbacks WSK en kernel para periféricos HID/gaming/SimRacing).
- 📊 **Medidor de Latencia en Vivo (`[ms]`) y Renombrado de Dispositivos (`✏️`)** - Indicador de ping TCP en tiempo real junto a cada servidor descubierto y nombres personalizados persistentes por periférico.
- 🔌 **Desconexión Limpia y Ciclo de Vida de Drivers Blindado** - Libera automáticamente los puertos USB remotos al salir o desinstalar y gestiona de forma segura el nodo virtual `ROOT\USBIP_WIN2\UDE` sin crear controladoras duplicadas ni reiniciar concentradores USB físicos.
- 🛡️ **Drivers Certificados WHQL** - Controladores oficiales firmados por Microsoft para Windows 10/11 x64 compatibles con Aislamiento de Núcleo (Core Isolation / HVCI).
- 📦 **Modo Portable e Instalador Híbrido** - Las versiones ZIP guardan la configuración localmente; el instalador Inno Setup usa `%AppData%\USBAnywhere` e inicia silenciosamente en bandeja (`--minimized`).
- 📚 **Base de Datos USB Actualizada** - Base `usb.ids` con más de 25.000 dispositivos identificados.

---

## ✨ Características Principales

- 🔍 **Autodescubrimiento** - Escanea servidores USB/IP en tu red local en segundos con indicador de latencia `[ms]`
- ⚡ **Aprovisionamiento SSH 1-Click** - Instala e inicia `usbipd` en tu Raspberry Pi/Linux automáticamente
- 🌐 **Conexión por Internet** - Conecta desde cualquier lugar vía Tailscale, ZeroTier o WireGuard (NAT traversal)
- 🏎️ **Latencia Ajustable** - Botón en barra de estado para conmutar entre Zero-Copy y Low-Latency WSK
- 🔌 **Conexión en 1 Click y Desconexión Segura** - Libera puertos automáticamente al cerrar la app
- ⭐ **Favoritos y Auto-Reconexión** - Guarda dispositivos con reconexión automática (3 intentos) y perfil de latencia individual
- 📋 **Información de Hardware y Renombrado** - VID:PID, fabricante y producto de más de 25.000 dispositivos + nombres personalizados
- 🎨 **GUI Nativa WPF Moderna** - Interfaz fluida en .NET 9 con temas oscuro y claro en caliente
- 🌐 **Multi-idioma** - Español e Inglés conmutables en tiempo real
- 🔄 **Auto-actualización Segura** - Descarga e instalación verificada mediante sumas criptográficas SHA-256 (`SHA256SUMS.txt`)

---

## 📦 Instalación

### Opción 1: Portable (Sin instalación)
1. Descarga desde [USBAnywhere Downloads](https://github.com/oscarlosu-lab/USBAnywhere-Downloads/releases/tag/client-v3.0.1):
   - **Windows x64**: `USBAnywhere-v3.0.1-x64.zip`
   - **Windows ARM64**: `USBAnywhere-v3.0.1-arm64.zip` (Surface Pro X, Snapdragon X Elite, etc.)
2. Extrae el ZIP en cualquier carpeta
3. Ejecuta `USBAnywhere.exe` como Administrador
4. ¡Listo!

### Opción 2: Instalador (x64 Recomendado)
1. Descarga `USBAnywhere_Setup_v3.0.1.exe` desde [USBAnywhere Downloads](https://github.com/oscarlosu-lab/USBAnywhere-Downloads/releases/tag/client-v3.0.1)
2. Ejecuta el instalador como Administrador
3. Sigue el asistente de instalación

### ⚠️ Usuarios ARM64
Los drivers ARM64 son **test-signed**. Ver `README_ARM64.md` en el ZIP para instrucciones sobre cómo habilitar Windows Test Mode.

---

## 🚀 Uso Rápido

1. **Escanear** - Click en `🔍 Escanear` para encontrar servidores en tu red
2. **Listar** - Click en `📋 Listar` para ver los dispositivos USB disponibles
3. **Conectar** - Doble-click en un dispositivo o click derecho → Conectar
4. **Desconectar** - Click derecho → Desconectar

### ⚡ Configurar Servidor Linux / Raspberry Pi en 1-Click
1. Haz click en `🔗 SSH` en la barra superior
2. Introduce el usuario de tu Raspberry Pi / Linux (ej. `pi` o `root`) y su IP
3. Pulsa **`⚡ 1-Click Auto-Instalar`** — instalará `usbip`, activará el módulo del kernel de forma persistente, iniciará el demonio y listará tus puertos USB automáticamente.

**O ejecuta el Instalador Plug & Play de 1 Línea en tu Linux / Raspberry Pi:**
```bash
curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere/main/scripts/install-server.sh | sudo bash
```
> 💡 **100% Plug & Play (¡Igual que VirtualHere!):** Instala `usbipd`, crea el servicio `systemd` y añade una **regla `udev` hotplug** que **auto-exporta automáticamente cualquier USB en cuanto lo enchufas a la Raspberry Pi/Linux**, sin necesidad de escribir `usbip bind` a mano.

### 🐳 Servidor Docker / HomeLab / NAS (`docker-compose.yml`)
Despliega un servidor USB/IP Plug & Play en **Unraid, TrueNAS, Synology o Proxmox** usando [`docker/docker-compose.yml`](docker/docker-compose.yml):
```bash
git clone --recursive https://github.com/oscarlosu-lab/USBAnywhere.git && cd USBAnywhere/docker && docker compose up -d
```

### 🎮 Automatización CLI para Moonlight / Sunshine / Playnite
Conecta automáticamente tu mando, volante o auriculares al iniciar un juego en streaming:
```powershell
USBAnywhere.exe --minimized --connect 192.168.1.50 1-1.4
```

### 🌐 Conexión por Internet (VPN)
1. Instala **[Tailscale](https://tailscale.com/download)** en Windows y en tu servidor
2. Click en `● VPN` para escanear peers con USB/IP activo (puerto 3240)
3. Selecciona el servidor remoto y pulsa Conectar

Ver [docs/VPN_INTERNET_ES.md](docs/VPN_INTERNET_ES.md) para la guía completa.

---

## 🐧 Configuración Manual del Servidor Linux (Opcional)

Compatible con **cualquier dispositivo Linux** con puertos USB (Raspberry Pi, Orange Pi, Banana Pi, OpenWRT, CrealityBox, Debian/Ubuntu x86/ARM):

**Debian / Ubuntu / Raspberry Pi OS:**
```bash
sudo apt update && sudo apt install -y usbip hwdata usbutils
sudo modprobe usbip_host
sudo usbipd -D
sudo usbip list -l
sudo usbip bind -b 1-1.4  # Sustituye por tu bus-id
```

**OpenWRT:**
```bash
opkg update && opkg install usbip-server kmod-usb-ohci
usbipd -D
```

Ver [docs/RASPBERRY_PI_SERVER_EN.md](docs/RASPBERRY_PI_SERVER_EN.md) para la guía detallada.

---

---

## 🔐 Firma de Código (Code Signing)

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

Los binarios oficiales de Windows se firman criptográficamente utilizando la SignPath Foundation para verificar su autenticidad, procedencia e integridad. Consulta nuestra [Política de Firma de Código](CODE_SIGNING_POLICY.md).

---

## 📄 Licencia

Este proyecto está bajo la licencia **GNU General Public License v3.0 (GPLv3)**.

> **USBAnywhere es 100% software libre y código abierto.** Eres libre de ejecutar, estudiar, compartir y modificar el software bajo los términos de la Licencia Pública General de GNU publicada por la Free Software Foundation.

Consulta el archivo [LICENSE](LICENSE) para ver el texto completo de la licencia.

## 🔗 Ecosistema USBAnywhere

| Componente | Plataforma / Rol | Descripción |
|---|---|---|
| **[USBAnywhere Cliente](https://github.com/oscarlosu-lab/USBAnywhere)** | 🖥️ Windows 10/11 (x64 / ARM64) | Conecta dispositivos USB remotos con drivers WHQL y motor WSK |
| **[USBAnywhere Servidor](https://github.com/oscarlosu-lab/USBAnywhere-Server)** | 🌐 Windows 10/11 (.NET 9.0) | Comparte periféricos USB locales por red con GUI y Auto-Bind |
| **[USBAnywhere Android](https://github.com/oscarlosu-lab/USBAnywhere-Android)** | 📱 Android y Android TV | Servidor USB/IP rootless, integración Sunshine y hápticos DualSense |
| **[Web Oficial](https://oscarlosu-lab.github.io/USBAnywhere/)** | 🌍 Portal Web | Guías interactivas, documentación y centro de descargas |
| **[Comunidad Reddit](https://www.reddit.com/r/UsbAnywhere/)** | 💬 r/UsbAnywhere | Soporte, debates, configuraciones de la comunidad y anuncios |
| **[Apoyar en Ko-fi](https://ko-fi.com/oscarlosu)** | ☕ Comunidad | Apoya el hosting, pruebas de hardware y desarrollo libre |

---

## ☕ Apoya el Proyecto

Si USBAnywhere te ha servido para tu simulador, streaming o taller maker sin cables por el suelo, considera [invitarnos a un café en Ko-fi](https://ko-fi.com/oscarlosu). Tu aportación apoya directamente los certificados de firma digital de drivers, nuevo hardware de prueba (SBCs, volantes, mandos) e infraestructura.

---

## 🙏 Créditos

| Proyecto | Autor | Contribución |
|----------|-------|--------------|
| [usbip-win2](https://github.com/vadimgrn/usbip-win2) | **Vadim Grn** | Drivers USB/IP firmados por Microsoft (WHQL certified). Core del cliente Windows. |
| [OctoWrt](https://github.com/ihrapsa/OctoWrt) | **ihrapsa** | Guía original para OpenWrt en CrealityBox. Inspiración para soporte de dispositivos embebidos. |
| [OctoWrt Fork](https://github.com/shivajiva101/OctoWrt) | **ShivaJiva** | Mantenimiento activo de OctoWrt. Releases actualizados para CrealityBox. |
| [USB/IP](https://www.kernel.org/doc/html/latest/usb/usbip_protocol.html) | **Linux Kernel** | Protocolo original USB/IP |
| **USBAnywhere** | **SnakeFoxu (OscarLosu)** | GUI .NET 9 WPF, Auto-Setup SSH 1-Click, integración VPN, modos de latencia y documentación |
