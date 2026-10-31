# 🥽 Guía de Optimización: VR, HTC Vive Facial Tracker y Dispositivos de Alto Ancho de Banda

🌐 **Idioma / Language:** **Español** | [English](VR_FACIAL_TRACKER_GUIDE_EN.md)

---

## 🎯 Introducción y Caso de Uso

El seguimiento facial en Realidad Virtual (**VR Facial Tracking**) para plataformas como **VRChat** y **Resonite** mediante herramientas como **VRCFaceTracking (VRCFT)** requiere transmitir streams constantes de vídeo de alta velocidad desde cámaras infrarrojas montadas en el visor hacia el PC gamer.

El hardware más popular para esta tarea es el **HTC Vive Facial Tracker** (ID de hardware `0bb4:0320`), que incorpora dos cámaras IR capturando a 60 fotogramas por segundo sin compresión.

```
┌─────────────────────────────────┐                 ┌─────────────────────────────────┐
│        VISOR VR / HEADSET       │   Wi-Fi 5/6GHz  │            PC GAMER             │
│  HTC Vive Facial Tracker (60Hz) │ ──────────────> │   USBAnywhere Client [Zero-Copy]│
│   USBAnywhere Android/Linux Srv │                 │   VRCFaceTracking + SRAnipal    │
└─────────────────────────────────┘                 └─────────────────────────────────┘
```

### ⚠️ El Cuello de Botella de VirtualHere
En utilidades tradicionales de USB sobre IP como *VirtualHere*:
* El stream de vídeo de las cámaras IR satura los sockets estándar de red debido a búferes de copia intermedia en espacio de usuario.
* El algoritmo de Nagle y el jitter de la red Wi-Fi provocan **congelamientos periódicos**, desconexiones del tracker y retraso acumulativo en las expresiones del avatar.
* VirtualHere impone un límite de 1 solo dispositivo gratuito y licencias costosas por servidor.

### ⚡ La Solución de USBAnywhere
1. **Modo Zero-Copy (`⚡ Modo: Zero-Copy`)**: El tráfico de paquetes URB de la cámara se vuelca directamente entre el subsistema de red y el controlador de dispositivo virtual en el kernel de Windows, eliminando copias intermedias en CPU y reduciendo el overhead de procesamiento a menos de 1 ms.
2. **Pipeline de Red de Mínima Latencia (WSK)**: Utiliza sockets Winsock Kernel con `TCP_NODELAY` activado de forma forzada, enviando cada frame del tracker al instante.
3. **Inicio Desatendido y Minimizado**: Opción nativa **Iniciar minimizado en bandeja** (`Start Minimized to Tray`) para que el PC gamer conecte el tracker de forma completamente transparente y desatendida.
4. **Sin Límites de Dispositivos**: Puedes transmitir simultáneamente el Facial Tracker, un dongle de Bluetooth para mandos Index, y sensores auxiliares sin ningún coste ni límite.

---

## 🛠️ Topologías de Conexión Recomendadas

### Opción A: Visor Autónomo (Steam Frame / Quest 2/3/Pro / Vive XR Elite / Focus 3)
* **Servidor**: El visor ejecuta la app **USBAnywhere Android** en segundo plano (rootless via `UsbManager`).
* **Hardware**: El HTC Vive Facial Tracker va conectado al puerto USB-C del visor (o mediante un adaptador OTG divisor de carga activa).
* **Cliente**: El PC ejecuta **USBAnywhere Client** para Windows.

### Opción B: Companion Raspberry Pi / Mini-PC en Bolsillo o Arnés
* **Servidor**: Raspberry Pi Zero 2W, Pi 4 o Pi 5 ejecutando el script `install-server-linux.sh`.
* **Hardware**: Alimentado con una batería externa de 5V 2.4A o 3A en el bolsillo del usuario, transmitiendo por Wi-Fi hacia el PC gamer.

---

## 📶 Calibración Óptima de la Red Wi-Fi

Para que una cámara de 60 Hz transmita de forma ininterrumpida sin micro-tirones:

| Parámetro | Valor Recomendado | Motivo |
| :--- | :--- | :--- |
| **Banda Wi-Fi** | **5 GHz (Wi-Fi 5/6) o 6 GHz (Wi-Fi 6E/7)** | 2.4 GHz tiene ancho de banda insuficiente y demasiadas interferencias. |
| **Ancho de Canal** | **80 MHz o 160 MHz** | Maximiza el throughput continuo de paquetes de imagen. |
| **Separación de SSIDs** | **SSID exclusivo para VR** | Evita que el visor conmute a la banda de 2.4 GHz dinámicamente. |
| **Canales DFS** | **Desactivados (usar canales 36-48 o 149-165)** | Los canales DFS saltan automáticamente ante señales de radar, cortando el streaming. |
| **Conexión del PC** | **Cable Ethernet Gigabit (1G / 2.5G)** | El PC **nunca** debe estar por Wi-Fi si el visor ya está en Wi-Fi. |
| **QoS / WMM** | **Habilitado en el router** | Prioriza paquetes interactivos de baja latencia. |

---

## 🚀 Paso a Paso: Configuración y Puesta en Marcha

### Paso 1: Conexión Física y Alimentación
1. Conecta el HTC Vive Facial Tracker al puerto USB-C del visor o Raspberry Pi.
2. ⚠️ **Atención a la alimentación**: Si el puerto USB no entrega al menos 500mA estables a 5V, el tracker se reiniciará constantemente y Windows lo detectará erróneamente en bucle como `HTC Boot`. Asegúrate de usar un cable o hub OTG de buena calidad.

### Paso 2: Configuración en USBAnywhere (PC Gamer)
1. Abre **USBAnywhere** en tu PC con Windows.
2. En la barra inferior, verifica que el modo esté en **`⚡ Modo: Zero-Copy`** (botón verde). Si está en naranja, haz clic sobre él para cambiarlo a Zero-Copy.
3. Activa la casilla de verificación en la esquina inferior izquierda:
   - ☑️ **Iniciar minimizado en bandeja**
4. Haz clic en **🔍 Escanear** o introduce la IP de tu visor / Raspberry Pi y pulsa **📋 Listar**.
5. Localiza el dispositivo `HTC Multimedia Camera` o ID `0bb4:0320`.
6. Haz clic derecho sobre él y selecciona:
   - **⭐ Añadir a Favoritos**
7. En el nodo **⭐ Favoritos**, asegúrate de que tiene activada la reconexión automática.
8. Haz doble clic sobre él o pulsa **🔗 Conectar**.

### Paso 3: Verificación en Windows
1. Abre el **Administrador de dispositivos** de Windows (`devmgmt.msc`).
2. Despliega la categoría **Cámaras** o **Dispositivos de imagen**.
3. Debe aparecer listado como **`HTC Multimedia Camera`** sin símbolos de advertencia amarillos.

### Paso 4: Configuración de VRCFaceTracking (VRCFT)
1. Inicia **SRAnipal Runtime** (si utilizas la suite oficial de HTC Vive Console).
2. Abre **VRCFaceTracking** (versión 5.0 o superior).
3. En la pestaña **Module Registry**, busca e instala el módulo:
   - `SRAnipal Tracking Module` (desarrollado por la comunidad VRCFT).
4. El indicador de estado de VRCFT debe cambiar a color verde (**Active**).
5. Abre **SteamVR** y **VRChat**: tus gestos labiales y mandibulares se reflejarán instantáneamente en tu avatar.

---

## 🔧 Solución de Problemas Frecuentes

### 1. El dispositivo aparece como `HTC Boot` y no como cámara
* **Causa**: El dispositivo se encuentra en modo bootloader de firmware o el puerto USB-C del visor tiene una caída de voltaje momentánea.
* **Solución**:
  - Desconecta y vuelve a conectar físicamente el tracker.
  - No desconectes si Windows está aplicando una actualización de firmware inicial.
  - Utiliza un splitter OTG con alimentación externa si el visor suministra poca energía por batería baja.

### 2. Microtirones o desconexiones intermitentes cada pocos minutos
* **Causa**: Conmutación de canal Wi-Fi en el router o coexistencia con dispositivos 2.4 GHz.
* **Solución**:
  - Asegúrate de estar en una banda de 5 GHz fija (canal 36, 40, 44 o 48).
  - En USBAnywhere, mantén el modo **Zero-Copy** activo.

### 3. Las expresiones no se mueven en VRChat pero la cámara está conectada
* **Causa**: Falta de iluminación en el rostro o módulo SRAnipal no inicializado.
* **Solución**:
  - Las cámaras infrarrojas duales del Vive Facial Tracker requieren iluminación ambiental básica en la habitación para calibrar la distancia labial.
  - Cierra y vuelve a abrir VRCFaceTracking como Administrador después de que USBAnywhere haya completado el montaje del USB.

---

## 🌟 Resumen de Ventajas para la Comunidad VR

| Característica | VirtualHere (Free/Paid) | USBAnywhere v3.0+ |
| :--- | :---: | :---: |
| **Licencia** | Propietario (49$/server) | **100% Gratuito y Open Source (GPLv3)** |
| **Límite de Dispositivos** | 1 dispositivo en versión gratis | **Ilimitados simultáneos** |
| **Modo Kernel Zero-Copy** | ❌ No disponible | **✅ Nativo (Drivers WHQL certificados)** |
| **Socket TCP_NODELAY WSK** | ⚠️ Parcial | **✅ Optimizado para 60 FPS VR Streams** |
| **Inicio Minimizado en Bandeja** | ⚠️ Requiere argumentos manuales | **✅ 1 Clic desde UI y Menú de Bandeja** |
| **Reconexión Automática Silenciosa** | ⚠️ Inconsistente tras reinicio | **✅ Motor de reconexión de favoritos resiliente** |
