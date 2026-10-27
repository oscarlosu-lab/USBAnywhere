# 📱 USBAnywhere — Windows 10/11 on ARM64 Instructions

English | [Español](#español)

---

## English

### ⚠️ Test-Signed Driver Notice for ARM64

While x64 drivers included with USBAnywhere are **Microsoft WHQL-certified**, the **ARM64** drivers for USB/IP (`usbip2_filter.sys` and `usbip2_ude.sys`) are currently **test-signed**.

Windows on ARM (Surface Pro X, ThinkPad X13s, Snapdragon X Elite laptops, and ARM64 VMs) requires enabling **Test Mode** (Test Signing) to load test-signed kernel-mode drivers.

### How to Enable Test Signing on Windows 11/10 (ARM64)

1. Open **Terminal**, **PowerShell**, or **Command Prompt** as **Administrator**.
2. Run the following command:
   ```cmd
   bcdedit /set testsigning on
   ```
3. Restart your computer.
4. After restarting, a faint watermark `"Test Mode"` will appear in the bottom-right corner of your desktop.
5. Launch `USBAnywhere.exe`. The drivers will load normally and you can connect any remote USB devices.

### How to Disable Test Signing (Revert to Standard Mode)

To restore normal driver enforcement at any time:
1. Open Administrator Command Prompt or PowerShell.
2. Run:
   ```cmd
   bcdedit /set testsigning off
   ```
3. Restart your computer.

---

## <a name="español"></a>Español

### ⚠️ Aviso sobre Drivers Test-Signed para ARM64

Mientras que los drivers x64 incluidos en USBAnywhere están **certificados por Microsoft WHQL**, los drivers para arquitecturas **ARM64** (`usbip2_filter.sys` y `usbip2_ude.sys`) están actualmente firmados en modo de pruebas (**test-signed**).

Windows en dispositivos ARM (Surface Pro X, ThinkPad X13s, portátiles con Snapdragon X Elite y máquinas virtuales ARM64) requiere activar el modo de pruebas (**Test Signing**) para cargar controladores en modo kernel con firma de pruebas.

### Cómo Activar el Modo de Pruebas en Windows 11/10 (ARM64)

1. Abre la **Terminal**, **PowerShell** o el **Símbolo del sistema** como **Administrador**.
2. Ejecuta el siguiente comando:
   ```cmd
   bcdedit /set testsigning on
   ```
3. Reinicia tu equipo.
4. Tras reiniciar, verás una marca de agua discreta indicando `"Modo de prueba"` en la esquina inferior derecha.
5. Abre `USBAnywhere.exe`. Los drivers se cargarán con normalidad y podrás conectar tus periféricos USB remotos.

### Cómo Desactivar el Modo de Pruebas (Volver al Estado Normal)

Para restablecer la política estándar de firmas en cualquier momento:
1. Abre el Símbolo del sistema como Administrador.
2. Ejecuta:
   ```cmd
   bcdedit /set testsigning off
   ```
3. Reinicia tu equipo.
