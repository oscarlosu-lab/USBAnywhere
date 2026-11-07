# 🍓 Servidor USB/IP en Raspberry Pi

🌐 **Idioma / Language:** [English](RASPBERRY_PI_SERVER_EN.md) | **Español**

Guía paso a paso para configurar un servidor USB/IP en Raspberry Pi OS.

## 📋 Requisitos

- Raspberry Pi (cualquier modelo con USB)
- Raspberry Pi OS (Debian-based)
- Conexión a red local
- Dispositivo USB para compartir

## ⚡ Opción A: Instalador Plug & Play en 1 Línea (Recomendado)

Ejecuta este único comando en tu Raspberry Pi para instalar `usbip`, configurar el servicio `systemd` y activar la **auto-exportación `udev` hotplug** (cualquier USB que enchufes en la Pi aparecerá automáticamente en Windows igual que en VirtualHere):

```bash
curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere-Server/main/scripts/install-server-linux.sh | sudo bash
```

---

## 🚀 Opción B: Instalación Manual

```bash
# 1. Actualizar sistema
sudo apt update && sudo apt upgrade -y

# 2. Instalar USB/IP
sudo apt install -y linux-tools-generic

# 3. Cargar módulo del kernel
sudo modprobe usbip_host

# 4. Iniciar daemon
sudo usbipd -D
```

## 📱 Compartir un Dispositivo USB

### Paso 1: Ver dispositivos disponibles
```bash
sudo usbip list -l
```

Salida ejemplo:
```
 - busid 1-1.4 (0bda:8152)
   Realtek Semiconductor Corp. : RTL8152 Fast Ethernet Adapter
```

### Paso 2: Compartir dispositivo (bind)
```bash
sudo usbip bind -b 1-1.4
```

### Paso 3: Verificar que está compartido
```bash
sudo usbip list -l
```
Debería mostrar `(attached)` junto al dispositivo.

## 🔄 Hacer Persistente (Autostart)

### Opción 1: Crear servicio systemd

```bash
sudo nano /etc/systemd/system/usbipd.service
```

Contenido:
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

Activar:
```bash
sudo systemctl daemon-reload
sudo systemctl enable usbipd
sudo systemctl start usbipd
```

### Opción 2: Añadir a rc.local

```bash
sudo nano /etc/rc.local
```

Añadir antes de `exit 0`:
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
# O si no funciona:
sudo apt install linux-tools-generic
```

### "Cannot find device"
```bash
# Ver todos los USB conectados
lsusb
# Ver detalles
sudo usbip list -l
```

### "Device busy"
```bash
# Desmontar si está montado
sudo umount /dev/sdX
# Luego hacer bind
sudo usbip bind -b 1-1.4
```

### "Connection refused" desde Windows
```bash
# Verificar que daemon está corriendo
sudo systemctl status usbipd
# O verificar proceso
ps aux | grep usbipd
```

## 🔌 Descompartir Dispositivo

```bash
sudo usbip unbind -b 1-1.4
```

## 📊 Ver Estado

```bash
# Dispositivos compartidos
sudo usbip list -l

# Conexiones activas
sudo usbip port

# Logs
journalctl -u usbipd -f
```

## 🔥 Firewall

Si tienes firewall activo, abre el puerto 3240:
```bash
sudo ufw allow 3240/tcp
```

---

---

## 🎮 Mandos DualSense y Gamepads con Audio (Prevención de Kernel Oops)

El Sony **DualSense** (`054c:0ce6`), **DualSense Edge** (`054c:0df2`) y mandos similares son dispositivos USB compuestos que combinan una interfaz HID estándar (botones, gatillos, palancas, touchpad, giroscopio, vibración háptica) con interfaces de audio USB isócrono (micrófono y altavoz).

En placas ARM de bajo consumo (como Raspberry Pi Zero 2 W) con kernel Linux 6.x, el envío de paquetes isócronos a través de USB/IP puede provocar un *Kernel Oops* (`stub_send_ret_submit` en el módulo `usbip_host`).

USBAnywhere instala automáticamente la regla de aislamiento de audio durante la ejecución de `install-server-linux.sh`. También puedes gestionarlo en cualquier momento mediante:

```bash
# Activar mitigación (mantiene el Gamepad HID activo y desautoriza interfaces de audio)
sudo usbanywhere-ctl fix-dualsense enable

# Ver estado
usbanywhere-ctl fix-dualsense status

# Desactivar mitigación
sudo usbanywhere-ctl fix-dualsense disable
```

O crear manualmente la regla persistente de udev:

```bash
sudo bash -c 'cat << "EOF" > /etc/udev/rules.d/99-usbanywhere-dualsense-fix.rules
# Desautorizar interfaces de audio de DualSense para prevenir Oops en usbip_host
ACTION=="add", SUBSYSTEM=="usb", ATTR{idVendor}=="054c", ATTR{idProduct}=="0ce6", ATTR{bInterfaceNumber}=="01", ATTR{authorized}="0"
ACTION=="add", SUBSYSTEM=="usb", ATTR{idVendor}=="054c", ATTR{idProduct}=="0ce6", ATTR{bInterfaceNumber}=="02", ATTR{authorized}="0"
ACTION=="add", SUBSYSTEM=="usb", ATTR{idVendor}=="054c", ATTR{idProduct}=="0df2", ATTR{bInterfaceNumber}=="01", ATTR{authorized}="0"
ACTION=="add", SUBSYSTEM=="usb", ATTR{idVendor}=="054c", ATTR{idProduct}=="0df2", ATTR{bInterfaceNumber}=="02", ATTR{authorized}="0"
EOF'

sudo udevadm control --reload-rules && sudo udevadm trigger
```

## 📝 Notas

- El `bus-id` (ej: `1-1.4`) puede cambiar si reconectas el dispositivo
- Algunos dispositivos USB no son compatibles con USB/IP
- El servidor debe estar en la misma red que el cliente Windows
