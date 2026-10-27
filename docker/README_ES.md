# 🐳 Servidor USBAnywhere en Docker

🌐 **Idioma / Language:** [English](README.md) | **Español**

Servidor USB/IP contenedorizado basado en Linux, diseñado para exportar periféricos USB físicos del host a través de la red con mínimo consumo de recursos.

[![Licencia](https://img.shields.io/badge/Licencia-GPLv3-blue.svg?style=flat-square)](https://www.gnu.org/licenses/gpl-3.0)
[![Docker](https://img.shields.io/badge/Docker-Contenedor-blue?style=flat-square&logo=docker)](https://www.docker.com/)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Apoyar%20Proyecto-FF5E5B?style=flat-square&logo=ko-fi&logoColor=white)](https://ko-fi.com/oscarlosu)

---

## 🚀 Inicio Rápido

### 1. Con Docker Compose (Recomendado)
```bash
cd docker
docker compose up -d
```

### 2. Con Docker Run
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

## ⚙️ Variables de Entorno y Configuración

| Variable | Valor por Defecto | Descripción |
|---|---|---|
| `AUTO_BIND_ALL` | `true` | Exporta automáticamente todos los dispositivos USB conectados (excepto concentradores raíz) |
| `BIND_BUSIDS` | `""` | Lista separada por comas de bus IDs específicos para exportar (ej. `1-1.2,1-1.4`) |
| `TCP_PORT` | `3240` | Puerto de escucha de red para el protocolo USB/IP |

---

## 🛡️ Requisitos del Host

Dado que USB/IP requiere interactuar con el bus USB del núcleo del host, el contenedor necesita:
1. El flag `--privileged` o la capacidad `CAP_SYS_ADMIN`.
2. Montaje directo de los buses USB: `-v /dev/bus/usb:/dev/bus/usb`.
3. Módulos del kernel cargados en el host:
   ```bash
   sudo modprobe usbip_core
   sudo modprobe usbip_host
   ```

---

## 📄 Licencia

Licencia GPL-3.0. Desarrollado por [oscarlosu-lab](https://github.com/oscarlosu-lab).
