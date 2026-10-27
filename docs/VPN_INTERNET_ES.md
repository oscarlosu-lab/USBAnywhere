# 🌐 Conexión por Internet (VPN)

🌐 **Idioma / Language:** [English](VPN_INTERNET_EN.md) | **Español**

> **Nuevo en v1.7.0** - Conecta dispositivos USB remotos a través de Internet usando Tailscale o ZeroTier.

## 📋 Resumen

USBAnywhere ahora soporta conexión a servidores USB/IP **fuera de tu red local** usando VPNs mesh gratuitas. Esto significa que puedes:

- Conectar una impresora USB de tu oficina desde casa
- Acceder a dispositivos USB de una Raspberry Pi en otra ciudad
- Compartir dongles de licencia entre ubicaciones remotas

## 🔧 Requisitos

1. **Tailscale** (recomendado) o **ZeroTier** instalado en:
   - Tu PC Windows (cliente)
   - El servidor USB/IP (Raspberry Pi, Linux, etc.)
2. Ambos dispositivos unidos a la **misma red VPN**
3. Servidor USB/IP funcionando en el servidor remoto

## 📦 Opción 1: Tailscale (Recomendado)

### ¿Por qué Tailscale?
- ✅ Basado en **WireGuard** (rápido y seguro)
- ✅ **100 dispositivos gratis**
- ✅ Configuración en 2 minutos
- ✅ Funciona detrás de NAT sin port forwarding

### Instalación en Windows

1. Descarga Tailscale desde [tailscale.com/download](https://tailscale.com/download)
2. Instala y haz login con Google, Microsoft o GitHub
3. ¡Listo! Tu PC ya tiene una IP Tailscale (ej: `100.x.x.x`)

### Instalación en Raspberry Pi / Linux

```bash
# Instalar Tailscale
curl -fsSL https://tailscale.com/install.sh | sh

# Iniciar y autenticar
sudo tailscale up

# Verificar IP asignada
tailscale ip -4
```

### Configurar servidor USB/IP en la Pi

```bash
# Instalar USB/IP
sudo apt update && sudo apt install -y linux-tools-generic hwdata

# Cargar módulo
sudo modprobe usbip_host

# Iniciar daemon
sudo usbipd -D

# Ver dispositivos disponibles
usbip list -l

# Exportar un dispositivo (ej: 1-1.4)
sudo usbip bind -b 1-1.4
```

## 📦 Opción 2: ZeroTier

### ¿Por qué ZeroTier?
- ✅ **25 dispositivos gratis**
- ✅ Self-hosted disponible
- ✅ Buena alternativa a Tailscale

### Instalación en Windows

1. Descarga desde [zerotier.com/download](https://www.zerotier.com/download/)
2. Instala y crea una cuenta
3. Crea una red en [my.zerotier.com](https://my.zerotier.com)
4. Une tu PC a la red con el Network ID

### Instalación en Raspberry Pi / Linux

```bash
# Instalar ZeroTier
curl -s https://install.zerotier.com | sudo bash

# Unirse a la red (reemplaza NETWORK_ID)
sudo zerotier-cli join NETWORK_ID

# Verificar estado
sudo zerotier-cli status
```

## 🚀 Uso en USBAnywhere

1. **Abre USBAnywhere**
2. **Click en `🌐 VPN`** (botón teal)
3. La app detectará automáticamente:
   - Si tienes Tailscale instalado
   - Si tienes ZeroTier instalado
4. **Escanea peers VPN** buscando servidores USB/IP activos
5. **Selecciona un peer** de la lista
6. **Click en Conectar**
7. La IP del peer se copiará al campo servidor
8. Usa **🔄 Listar** para ver dispositivos disponibles

## 🔍 Cómo funciona

```
┌─────────────────┐         ┌─────────────────┐
│   Tu PC         │         │  Raspberry Pi   │
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

1. **Tailscale/ZeroTier** crea un túnel VPN entre ambos dispositivos
2. **USBAnywhere** detecta los peers de la VPN
3. **Escanea puerto 3240** en cada peer para encontrar servidores USB/IP
4. **Conecta** usando el protocolo USB/IP estándar sobre la VPN

## ❓ Solución de Problemas

### No se detecta Tailscale/ZeroTier
- Verifica que esté instalado y corriendo
- Comprueba que estés autenticado (`tailscale status`)

### No aparecen peers
- Asegúrate que ambos dispositivos estén en la misma red VPN
- Verifica que el servidor USB/IP esté corriendo (`usbipd`)
- Comprueba el firewall no bloquee el puerto 3240

### Conexión lenta
- Es normal si hay mucha distancia geográfica
- Tailscale usa "DERP" relays si no puede hacer hole punching
- Para mejor rendimiento, asegura que ambos dispositivos tengan buena conexión

### "USB/IP: ❌" en la lista de peers
- El servidor USB/IP no está corriendo en ese peer
- Ejecuta `sudo usbipd -D` en el servidor

## 📊 Comparación de Costos

| Solución | Precio | Dispositivos | Internet |
|----------|--------|--------------|----------|
| **USBAnywhere + Tailscale** | **GRATIS** | 100 | ✅ |
| **USBAnywhere + ZeroTier** | **GRATIS** | 25 | ✅ |
| VirtualHere | $49 USD | Ilimitado | ✅ |
| FlexiHub | $14/mes | Por dispositivo | ✅ |
| USB Network Gate | $159 USD | Por servidor | ✅ |

## 🔗 Enlaces Útiles

- [Tailscale - Descargar](https://tailscale.com/download)
- [ZeroTier - Descargar](https://www.zerotier.com/download/)
- [Configurar Servidor Raspberry Pi](RASPBERRY_PI_SERVER.md)
- [Manual de Usuario](USAGE.md)
