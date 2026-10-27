# 🛡️ Política de Seguridad — USBAnywhere

🌐 **Idioma / Language:** [English](SECURITY.md) | **Español**

## Versiones Soportadas

| Versión | Estado              | Comentarios |
| ------- | ------------------- | ----------- |
| 3.0.x   | :white_check_mark:  | Activa (Versión actual: v3.0.1) |
| < 3.0   | :x:                 | Sin soporte (Actualizar a USBAnywhere v3.0.1) |

---

## Arquitectura de Seguridad y Blindaje Zero-Trust

**USBAnywhere** está diseñado siguiendo un modelo de defensa en profundidad y verificación estricta Zero-Trust en todo el ecosistema:

### 1. Sanitización de Argumentos por Lista Blanca (`InputValidator`)
- Todas las direcciones IPv4, nombres de host, identificadores USB `bus-id` y puertos TCP se validan rigurosamente mediante expresiones regulares restrictivas antes de invocar cualquier comando del sistema.
- Los subprocesos externos (`usbipw.exe`, `devnode.exe`, `pnputil.exe`) se ejecutan con `UseShellExecute = false`, impidiendo ataques de inyección de comandos en la consola de comandos.

### 2. Firmas Digitales y Verificación Authenticode (`SignatureVerifier`)
- Todos los ejecutables nativos empaquetados (`usbipw.exe`, `devnode.exe`, `USBAnywhere.exe`) se verifican contra firmas digitales Microsoft Authenticode válidas.
- La integridad de los binarios distribuidos se coteja contra manifiestos criptográficos antes de ejecutar operaciones de conexión o desconexión PnP.

### 3. Actualizador Criptográfico con Fallo Seguro (`UpdateService`)
- El motor de actualización integrado se comunica exclusivamente mediante HTTPS y restringe las descargas a endpoints oficiales de GitHub (`github.com`, `objects.githubusercontent.com`).
- Los paquetes de actualización se verifican contra el archivo de manifiesto oficial `SHA256SUMS.txt`. Si el hash SHA-256 no coincide de forma exacta, el archivo se elimina de inmediato y la actualización se cancela sin ejecutar el instalador.

### 4. Controladores Certificados por Microsoft WHQL
- Emplea los controladores firmados por Microsoft WHQL `usbip2_ude.sys` y `usbip2_filter.sys`, totalmente compatibles con **Aislamiento del núcleo / Integridad de memoria (HVCI)** en Windows 10 y 11.
- No requiere activar el modo de pruebas de Windows (*Test Mode*) en arquitecturas x64 estándar.

### 5. Modelos de Seguridad en Servidores Multiplataforma
- **Servidor Windows**: La regla automática del Firewall de Windows para el puerto 3240 se acota exclusivamente a redes **Privadas** y de **Dominio**, evitando la exposición del puerto en redes Wi-Fi públicas no confiables.
- **Servidor macOS**: Diseñado mediante **Userspace IOKit** (`IOKit.framework`). Opera sin extensiones de kernel (`.kext`) y preserva la integridad total de **SIP (System Integrity Protection)** en Apple Silicon. El demonio se ejecuta con privilegios controlados mediante `launchd`.
- **Servidor Linux**: Se ejecuta bajo una unidad de servicio systemd aislada y restringe la exportación de periféricos mediante reglas udev específicas.

---

## Notificación de Vulnerabilidades

Si descubres una vulnerabilidad o fallo de seguridad, por favor infórmalo de forma responsable:
- **GitHub Security Advisory**: Abre un aviso privado en [USBAnywhere Security Advisories](https://github.com/oscarlosu-lab/USBAnywhere/security/advisories/new)
- **Contacto**: Contacta con los desarrolladores en [oscarlosu-lab](https://github.com/oscarlosu-lab).

Agradecemos la divulgación responsable y nos comprometemos a investigar y corregir cualquier reporte con la máxima celeridad.
