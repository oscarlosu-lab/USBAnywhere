# 📖 Manual de Usuario de USBAnywhere v3.0.1

🌐 **Idioma / Language:** [English](USAGE_EN.md) | **Español**

## Interfaz Principal

```
╔═════════════════════════════════════════════════════════════════════════╗
║ 🌐 USBAnywhere v3.0 — Virtual USB Hub   [🌙] [ESP] [⬆️ Actualizar] 🟡🟢🔴║
╠═════════════════════════════════════════════════════════════════════════╣
║  Servidor: ┌──────────────┐ ┌──────────┐┌────────┐┌──────┐┌──────────┐  ║
║            │192.168.1.50  │ │🔍Escanear││📋Listar││🔗 SSH││ ● VPN    │  ║
║            └──────────────┘ └──────────┘└────────┘└──────┘└──────────┘  ║
╠═════════════════════════════════════════════════════════════════════════╣
║  🖥️ USB Hubs (3)                                                        ║
║   ├─ 🖧 192.168.1.50 (2) [2ms]                                          ║
║   │   ├─ 🎮 1-1.2: Sony Corp. : DualSense Wireless Controller (054c:0ce6)║
║   │   └─ 🏎️ 1-1.4: Logitech, Inc. : G29 Driving Force Wheel (046d:c24f) ║
║   └─ 🖧 192.168.1.105 (1) [4ms]                                         ║
║       └─ 💾 2-1: SanDisk Corp. : Ultra Flair 64GB USB 3.0 (0781:5591)   ║
║                                                                         ║
║  ☑️ Dispositivos Conectados (1)                                         ║
║   └─ 🔌 Puerto 00: 1-1.2: DualSense Wireless Controller ← 192.168.1.50  ║
║                                                                         ║
║  ⭐ Favoritos (2)                                                       ║
║   ├─ 🎮 1-1.2: DualSense Controller @ 192.168.1.50                      ║
║   └─ 🏎️ 1-1.4: G29 Driving Force @ 192.168.1.50                         ║
╠═════════════════════════════════════════════════════════════════════════╣
║ 📋 Log de Actividad                                          [Limpiar]  ║
╠─────────────────────────────────────────────────────────────────────────╣
║ [14:32:15] ☑ Conectado: 1-1.2 desde 192.168.1.50                        ║
║ [14:32:10] ℹ Escaneando red...                                          ║
║ [14:32:12] ☑ 2 servidor(es) encontrado(s)                               ║
╠═════════════════════════════════════════════════════════════════════════╣
║ ✓ Listo  [⚡ Modo: Zero-Copy]  Driver: Instalado ✓ [Instalar][Desinstalar]║
╚═════════════════════════════════════════════════════════════════════════╝
```

### Barra de Título (Estilo macOS)
```
┌────────────────────────────────────────────────────────────────────────┐
│ 🌐 USBAnywhere v3.0 — Virtual USB Hub   [🌙] [ESP] [⬆️ Actualizar] 🟡🟢🔴│
└────────────────────────────────────────────────────────────────────────┘
                                            │    │         │         │ │ │
                         Alternar Tema ─────┘    │         │         │ │ │
                         (Oscuro / Claro)        │         │         │ │ │
                                  Idioma ES/EN ──┘         │         │ │ │
                                            Buscar Update ─┘         │ │ │
                                                      Minimizar ─────┘ │ │
                                                      Maximizar ───────┘ │
                                                      Cerrar ────────────┘
```

### Panel de Servidor
```
┌──────────────────────────────────────────────────────────────────────┐
│ Servidor: [__192.168.1.50___] [🔍Escanear][📋Listar][🔗 SSH][ ● VPN] │
└──────────────────────────────────────────────────────────────────────┘
                │                    │          │        │       │
  IP del servidor│    Buscar en red ──┘          │        │       │
  (editable)     │    (escanea subred + [ms])    │        │       │
                 │                               │        │       │
                 │         Listar dispositivos ──┘        │       │
                 │         del servidor actual            │       │
                 │                                        │       │
                 │              Auto-Setup SSH 1-Click ───┘       │
                 │              para Raspberry Pi / Linux         │
                 │                                                │
                 │                     Conexión por Internet ─────┘
                 │                     (Tailscale/ZeroTier/WireGuard)
```

## Acciones Principales

### 🔍 Escanear
Busca servidores USB/IP en tu red local (puerto `3240`) y mide su latencia TCP en tiempo real (`[2ms]`).

### 📋 Listar
Muestra los dispositivos USB disponibles en todos los servidores descubiertos y en la IP indicada en el cuadro de texto.

### 🔗 SSH (y ⚡ 1-Click Auto-Instalar)
Abre el asistente SSH para Raspberry Pi / Linux. Incluye el botón **`⚡ 1-Click Auto-Instalar`** que instala `usbipd`, activa los módulos del kernel y configura la regla `udev` hotplug automáticamente.

### ● VPN
Escanea tus redes privadas virtuales (**Tailscale**, **ZeroTier** y **WireGuard**), comprueba qué nodos tienen el puerto `3240` activo y permite añadirlos al árbol principal con un clic.
Ver [VPN_INTERNET_ES.md](VPN_INTERNET_ES.md) para configuración completa.

---

## 🌐 Conexión a Servidores Multiplataforma

El cliente USBAnywhere se conecta de forma transparente a cualquier servidor USB/IP del ecosistema:

### 🍎 Conexión a un Servidor macOS
1. En tu Mac (Apple Silicon o Intel), ejecuta el instalador 1-click en Terminal:
   `ash
   curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere-Server/main/scripts/install-server-mac.sh | sudo bash
   `
2. En USBAnywhere Client, introduce la IP del Mac (ej. 192.168.1.50) y pulsa **📋 Listar**.
3. Selecciona tu joystick, volante de SimRacing o mando y pulsa **▶️ Conectar**.
4. Consulta el [Manual de macOS](https://github.com/oscarlosu-lab/USBAnywhere-Server/blob/main/docs/USAGE_MACOS_ES.md) para más opciones.

### 🖥️ Conexión a un Servidor Windows de Escritorio
1. Ejecuta USBAnywhere-Server.exe en el PC emisor y pulsa **📤 Compartir** en los dispositivos deseados.
2. En USBAnywhere Client, pulsa **🔍 Escanear** para auto-descubrirlo, o escribe su IP y pulsa **📋 Listar**.
3. Selecciona el dispositivo y pulsa **▶️ Conectar**.

### 🐧 Conexión a un Servidor Linux / Raspberry Pi
1. Utiliza el botón **⚡ Auto-Configurar 1-Click** en el asistente SSH, o ejecuta install-server.sh en el host.
2. El servidor aparecerá automáticamente en el árbol de dispositivos con medición de latencia en tiempo real.

### 📱 Conexión a un Servidor Android
1. Abre la app USBAnywhere Android en tu móvil o Android TV.
2. Introduce la IP del dispositivo Android en el cliente y pulsa **📋 Listar** para enlazar mandos o DACs.

---

## Menú Contextual (Clic Derecho)

```
┌───────────────────────────────────────────────┐
│ 🎮 1-1.2: DualSense Wireless Controller       │ ← Click derecho aquí
└───────────────────────────────────────────────┘
                        │
                        ▼
            ┌─────────────────────────┐
            │ ▶️ Conectar             │
            │ ⏹️ Desconectar          │
            ├─────────────────────────┤
            │ ⭐ Añadir a Favoritos   │
            │ ❌ Quitar de Favoritos  │
            │ 🗑️ Quitar servidor      │
            ├─────────────────────────┤
            │ 📋 Propiedades          │
            │ ✏️ Renombrar            │
            └─────────────────────────┘
```

| Opción | Descripción |
|--------|-------------|
| ▶️ Conectar | Conecta el dispositivo USB remoto (o favorito) a tu PC |
| ⏹️ Desconectar | Libera el dispositivo USB conectado |
| ⭐ Añadir a Favoritos | Guarda el dispositivo para reconexión rápida |
| ❌ Quitar de Favoritos | Elimina el dispositivo de la lista de favoritos |
| 🗑️ Quitar servidor | Elimina el servidor del árbol |
| 📋 Propiedades | Muestra información detallada (`VID:PID`, fabricante, producto, puerto local) |
| ✏️ Renombrar | Asigna un nombre personalizado persistente al dispositivo (`VID:PID`) |

## Atajos

- **Doble-click** en dispositivo (en `USB Hubs` o `Favoritos`) → Conectar
- **Doble-click** en dispositivo conectado → Desconectar
- **Enter** en campo IP → Listar dispositivos

## Favoritos y Modos de Rendimiento

Los favoritos se guardan de forma atómica en `config.json` y se reconectan automáticamente (hasta 3 intentos) si la conexión de red sufre microcortes.

### ⚡ Modos de Rendimiento USB/IP (Conmutador en Barra de Estado)

Puedes alternar el modo de recepción del driver en cualquier momento pulsando el botón **`[⚡ Modo: Zero-Copy]`** / **`[🏎️ Modo: Low-Latency]`** en la barra de estado inferior:

| Modo | Ideal para | Descripción técnica |
|---|---|---|
| **⚡ Modo: Zero-Copy** *(Predeterminado)* | Discos duros, pendrives, webcams HD, SDR | Utiliza buffers de red directos sin copia intermedia (`--receive-mode=zero-copy`), logrando el máximo ancho de banda posible. |
| **🏎️ Modo: Low-Latency** | Mandos gaming, volantes SimRacing, ratones, teclados, HID | Basado en callbacks de eventos WSK a nivel de kernel de Windows (`--receive-mode=low-latency`). Minimiza la latencia de entrada para periféricos interactivos de alta tasa de sondeo. |

### 🔌 Desconexión Limpia Automática

Al cerrar la aplicación, apagar el PC (`AutoDetachOnExit: true`) o desinstalar los drivers, USBAnywhere libera y desacopla ordenadamente todos los puertos USB activos antes de finalizar. Esto garantiza que el servidor Linux nunca deje los puertos en estado bloqueado (*busy*).

## 🖥️ System Tray (Bandeja del Sistema) y Arranque Desatendido

- **☑️ Iniciar minimizado en bandeja**: Casilla seleccionable en la barra inferior (y en el menú de bandeja) para que la aplicación arranque directamente oculta en segundo plano sin mostrar ventana al iniciar Windows, manteniendo los dispositivos USB conectados permanentemente.
- Al **minimizar** (o iniciar con `--minimized` / `-m`), la aplicación se oculta en la bandeja del sistema.
- **Doble-click** en el icono de la bandeja para restaurar la ventana en cualquier momento.
- **Click derecho** para menú rápido: `📡 Escanear red`, `☑️ Iniciar minimizado en bandeja`, `🔼 Mostrar USBAnywhere`, `❌ Salir`.

## 🔧 Barra de Estado y Ciclo de Vida de Drivers

```
╔══════════════════════════════════════════════════════════════════════════╗
║ ✓ Listo  [⚡ Modo: Zero-Copy]  Driver: Instalado ✓ [Instalar][Desinstalar]║
╚══════════════════════════════════════════════════════════════════════════╝
```

- **Instalar**: Limpia cualquier nodo virtual residual e instala los drivers certificados WHQL (`usbip2_filter.inf` y `usbip2_ude.inf`) creando el controlador emulado `ROOT\USBIP_WIN2\UDE`.
- **Desinstalar**: Desconecta primero los dispositivos USB/IP activos, elimina el nodo virtual UDE y limpia los paquetes del `DriverStore` de forma segura en dos fases sin afectar a tus concentradores USB 3.0 físicos ni a otros adaptadores virtuales (como Parsec).

---

## 📚 Más Información

- **[VR_FACIAL_TRACKER_GUIDE_ES.md](VR_FACIAL_TRACKER_GUIDE_ES.md)** - Guía de optimización para Realidad Virtual, HTC Vive Facial Tracker y Webcams (VRChat / VRCFaceTracking)
- **[README_ES.md](../README_ES.md)** - Instalación y características principales
- **[RASPBERRY_PI_SERVER_ES.md](RASPBERRY_PI_SERVER_ES.md)** - Configurar servidor en Raspberry Pi / Linux
- **[VPN_INTERNET_ES.md](VPN_INTERNET_ES.md)** - Conexión remota por Internet (Tailscale / ZeroTier / WireGuard)
- **[CHANGELOG.md](../CHANGELOG.md)** - Historial completo de versiones
