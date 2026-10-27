using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// Localization service - Complete bilingual translation dictionaries (ES/EN)
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _translations;

    public LocalizationService()
    {
        _translations = new Dictionary<string, Dictionary<string, string>>
        {
            ["es"] = new Dictionary<string, string>
            {
                // Window & Title Bar
                ["title_main"] = "USBAnywhere",
                ["subtitle_main"] = "USB Virtual sobre IP",
                ["tooltip_lang"] = "Cambiar idioma (ES/EN)",
                ["tooltip_theme"] = "Cambiar tema (Oscuro/Claro)",
                ["tooltip_update"] = "Buscar actualizaciones",
                ["tooltip_minimize"] = "Minimizar a la bandeja",
                ["tooltip_maximize"] = "Maximizar / Restaurar",
                ["tooltip_close"] = "Cerrar",

                // Buttons
                ["btn_scan"] = "🔍 Escanear",
                ["btn_refresh"] = "📋 Listar",
                ["btn_ssh"] = "🔗 SSH",
                ["btn_vpn"] = "📡 VPN",
                ["btn_update"] = "⬆️ Actualizar",
                ["btn_install_drivers"] = "Instalar",
                ["btn_uninstall_drivers"] = "Desinstalar",

                // Labels
                ["lbl_server"] = "Servidor:",
                ["lbl_drivers"] = "Drivers: ",

                // TreeView nodes
                ["node_hubs"] = "🖥️ Concentradores USB",
                ["node_connected"] = "☑️ Dispositivos Conectados",
                ["node_favorites"] = "⭐ Favoritos",

                // Status
                ["status_ready"] = "✓ Listo",
                ["status_scanning"] = "Escaneando...",
                ["status_listing"] = "Listando dispositivos en",
                ["status_listing_servers"] = "Listando dispositivos en {0} servidor(es)...",
                ["status_connecting"] = "Conectando",
                ["status_disconnecting"] = "Desconectando",
                ["status_servers_found"] = "servidor(es) encontrado(s)",
                ["status_devices_found"] = "dispositivo(s)",
                ["status_devices_on_servers"] = "✓ {0} dispositivo(s) en {1} servidor(es)",
                ["status_no_server"] = "No se encontró servidor en la subred",
                ["status_error"] = "Error",

                // Log
                ["log_title"] = "📋 Registro de Actividad",
                ["log_clear"] = "Limpiar",
                ["log_connected"] = "Conectado",
                ["log_disconnected"] = "Desconectado",
                ["log_scan_start"] = "Escaneando red...",
                ["log_scan_found"] = "{0} servidor(es) encontrado(s)",
                ["log_scan_none"] = "Ningún servidor encontrado",
                ["log_error"] = "Error",
                ["log_app_start"] = "Aplicación iniciada",
                ["log_detaching_on_exit"] = "Desconectando dispositivos activos antes de salir...",
                ["log_mode_lowlatency"] = "🏎️ Modo Low-Latency WSK activado (ideal para SimRacing / Mandos / HID)",
                ["log_mode_zerocopy"] = "⚡ Modo Zero-Copy activado (ideal para Almacenamiento / Cámaras)",
                ["log_version_compare"] = "Versión actual: v{0} | Última: v{1} | Actualización disponible: {2}",
                ["log_already_favorite"] = "El dispositivo ya está en favoritos",
                ["log_device_renamed"] = "Dispositivo renombrado: {0}",
                ["log_connection_lost_detail"] = "Conexión perdida: {0}/{1}",
                ["log_connection_restored_detail"] = "Conexión restaurada: {0}/{1}",

                // Context menu
                ["menu_connect"] = "▶️ Conectar",
                ["menu_disconnect"] = "⏹️ Desconectar",
                ["menu_add_favorite"] = "⭐ Añadir a Favoritos",
                ["menu_remove_favorite"] = "❌ Quitar de Favoritos",
                ["menu_remove_server"] = "🗑️ Quitar servidor",
                ["menu_properties"] = "📋 Propiedades",
                ["menu_rename"] = "✏️ Renombrar",
                ["driver_installed"] = "Instalado ✓",
                ["driver_not_instantiated"] = "No instanciado",
                ["driver_not_installed"] = "No instalado",
                ["mode_zerocopy"] = "⚡ Modo: Zero-Copy",
                ["mode_lowlatency"] = "🏎️ Modo: Low-Latency",

                // Update
                ["update_checking"] = "Buscando actualizaciones...",
                ["update_available"] = "Nueva versión disponible",
                ["update_current"] = "Tienes la última versión",
                ["update_download"] = "Descargar e instalar",
                ["update_error"] = "Error al buscar actualizaciones",
                ["update_downloading"] = "Descargando actualización...",
                ["msg_update_title"] = "Actualización Disponible",
                ["msg_update_prompt"] = "Nueva versión disponible: v{0}\n\n{1}\n\n¿Desea descargar e instalar la actualización?",

                // Device Properties & Rename Dialogs
                ["props_title"] = "Propiedades del Dispositivo",
                ["props_busid"] = "Bus ID",
                ["props_server"] = "Servidor",
                ["props_desc"] = "Descripción",
                ["props_vidpid"] = "VID:PID",
                ["props_vendor"] = "Fabricante",
                ["props_product"] = "Producto",
                ["rename_title"] = "Renombrar Dispositivo",
                ["rename_prompt"] = "Renombrar dispositivo {0}:{1}\n\nNuevo nombre:",

                // Dialogs
                ["enter_ip"] = "Introduce una IP de servidor",
                ["enter_ip_ssh"] = "Introduce la IP del servidor para SSH",
                ["app_already_running"] = "USBAnywhere ya está en ejecución.\nRevisa la bandeja del sistema.",
                ["app_admin_error"] = "No se pudo elevar a administrador:\n{0}",

                // SSH Dialog
                ["ssh_title"] = "SSH - Configurar Servidor USB/IP",
                ["ssh_instructions"] = "📋 Instrucciones para Linux / Raspberry Pi / Armbian:",
                ["ssh_step1"] = "1. Instalar paquetes:",
                ["ssh_step2"] = "2. Cargar módulo del kernel:",
                ["ssh_step3"] = "3. Iniciar servicio:",
                ["ssh_step4"] = "4. Ver dispositivos USB disponibles:",
                ["ssh_step5"] = "5. Exportar dispositivo (ej: 1-1.4):",
                ["ssh_step6"] = "6. Auto-inicio (o usa el botón 1-Click Auto-Instalar):",
                ["ssh_user"] = "Usuario:",
                ["ssh_connect"] = "🔗 Conectar",
                ["ssh_copy"] = "📋 Copiar comando",
                ["ssh_copied"] = "✓ ¡Copiado!",
                ["ssh_auto_setup"] = "⚡ 1-Click Auto-Instalar",
                ["ssh_close"] = "Cerrar",
                ["ssh_invalid_chars"] = "Se detectaron caracteres no válidos en el usuario o IP.",
                ["ssh_security_title"] = "Aviso de Seguridad",

                // VPN Dialog
                ["vpn_title"] = "VPN / Red Remota",
                ["vpn_status"] = "Estado de la red:",
                ["vpn_checking"] = "Comprobando...",
                ["vpn_online"] = "Conectado",
                ["vpn_offline"] = "Desconectado",
                ["vpn_peers"] = "Nodos (Peers) en la red:",
                ["vpn_no_peers"] = "No hay nodos en línea",
                ["vpn_add_all"] = "Añadir Todos",
                ["vpn_connect"] = "Conectar",
                ["vpn_info"] = "ℹ️ Conecta con tus servidores remotos vía Tailscale o ZeroTier",

                // System Tray
                ["tray_scan"] = "Escanear red",
                ["tray_show"] = "Mostrar USBAnywhere",
                ["tray_exit"] = "Salir",
                ["tray_minimized"] = "Minimizado en la bandeja del sistema",
                ["notify_connected"] = "Dispositivo conectado",
                ["notify_connection_lost"] = "Conexión perdida",
                ["notify_reconnected"] = "Reconexión exitosa",

                // Favorites
                ["log_favorite_added"] = "Dispositivo añadido a favoritos",
                ["log_favorite_removed"] = "Dispositivo eliminado de favoritos",

                // Activity Log Messages
                ["log_tray_init_error"] = "No se pudo inicializar el icono de la bandeja: {0}",
                ["log_scanning_subnet"] = "Escaneando subred {0}.0/24...",
                ["log_scan_complete"] = "Escaneo completado: encontrados {0} servidor(es)",
                ["log_server_found"] = "  → Servidor: {0}",
                ["log_devices_on_server"] = "{0} dispositivo(s) en {1}",
                ["log_devices_already_connected"] = "Encontrados {0} dispositivo(s) ya conectado(s)",
                ["log_load_connected_error"] = "No se pudieron cargar los dispositivos conectados: {0}",
                ["log_auto_connecting"] = "Auto-conectando {0} favorito(s)...",
                ["log_connected_favorite"] = "Conectado {0} ({1})",
                ["log_connection_failed"] = "Fallo al conectar {0}: {1}",
                ["log_connection_error"] = "Error conectando {0}: {1}",
                ["log_connecting_device"] = "Conectando {0} en {1}...",
                ["log_connection_success"] = "Conectado {0}",
                ["log_connection_general_failed"] = "Conexión fallida",
                ["log_disconnecting"] = "Desconectando {0}...",
                ["log_disconnected_port"] = "Desconectado puerto {0}",
                ["log_disconnect_failed"] = "Desconexión fallida",
                ["log_device_not_found"] = "Dispositivo no encontrado, puede que ya esté desconectado",
                ["log_listing_devices"] = "Listando dispositivos en {0} servidor(es)...",
                ["log_listed_devices"] = "Listados {0} dispositivos en {1} servidor(es)",
                ["log_checking_updates"] = "Buscando actualizaciones...",
                ["log_update_available"] = "Nueva versión disponible: {0}",
                ["log_downloading_update"] = "Descargando actualización...",
                ["log_update_failed"] = "Actualización fallida",
                ["log_latest_version"] = "Tienes la última versión (v{0})",
                ["log_installing_drivers"] = "Instalando drivers...",
                ["log_drivers_installed"] = "Drivers instalados exitosamente",
                ["log_install_drivers_failed"] = "Fallo al instalar drivers",
                ["log_uninstalling_drivers"] = "Desinstalando drivers...",
                ["log_drivers_uninstalled"] = "Drivers desinstalados exitosamente",
                ["log_uninstall_drivers_failed"] = "Fallo al desinstalar drivers",
            },

            ["en"] = new Dictionary<string, string>
            {
                // Window & Title Bar
                ["title_main"] = "USBAnywhere",
                ["subtitle_main"] = "Virtual USB over IP",
                ["tooltip_lang"] = "Switch language (ES/EN)",
                ["tooltip_theme"] = "Switch theme (Dark/Light)",
                ["tooltip_update"] = "Check for updates",
                ["tooltip_minimize"] = "Minimize to system tray",
                ["tooltip_maximize"] = "Maximize / Restore",
                ["tooltip_close"] = "Close",

                // Buttons
                ["btn_scan"] = "🔍 Scan",
                ["btn_refresh"] = "📋 List",
                ["btn_ssh"] = "🔗 SSH",
                ["btn_vpn"] = "📡 VPN",
                ["btn_update"] = "⬆️ Update",
                ["btn_install_drivers"] = "Install",
                ["btn_uninstall_drivers"] = "Uninstall",

                // Labels
                ["lbl_server"] = "Server:",
                ["lbl_drivers"] = "Drivers: ",

                // TreeView nodes
                ["node_hubs"] = "🖥️ USB Hubs",
                ["node_connected"] = "☑️ Connected Devices",
                ["node_favorites"] = "⭐ Favorites",

                // Status
                ["status_ready"] = "✓ Ready",
                ["status_scanning"] = "Scanning...",
                ["status_listing"] = "Listing devices on",
                ["status_listing_servers"] = "Listing devices on {0} server(s)...",
                ["status_connecting"] = "Connecting",
                ["status_disconnecting"] = "Disconnecting",
                ["status_servers_found"] = "server(s) found",
                ["status_devices_found"] = "device(s)",
                ["status_devices_on_servers"] = "✓ {0} device(s) on {1} server(s)",
                ["status_no_server"] = "No server found in subnet",
                ["status_error"] = "Error",

                // Log
                ["log_title"] = "📋 Activity Log",
                ["log_clear"] = "Clear",
                ["log_connected"] = "Connected",
                ["log_disconnected"] = "Disconnected",
                ["log_scan_start"] = "Scanning network...",
                ["log_scan_found"] = "{0} server(s) found",
                ["log_scan_none"] = "No servers found",
                ["log_error"] = "Error",
                ["log_app_start"] = "Application started",
                ["log_detaching_on_exit"] = "Detaching active USB devices before exit...",
                ["log_mode_lowlatency"] = "🏎️ Low-Latency WSK mode enabled (ideal for SimRacing / Controllers / HID)",
                ["log_mode_zerocopy"] = "⚡ Zero-Copy mode enabled (ideal for Storage / Webcams)",
                ["log_version_compare"] = "Current version: v{0} | Latest: v{1} | Update available: {2}",
                ["log_already_favorite"] = "Device is already in favorites",
                ["log_device_renamed"] = "Device renamed: {0}",
                ["log_connection_lost_detail"] = "Connection lost: {0}/{1}",
                ["log_connection_restored_detail"] = "Connection restored: {0}/{1}",

                // Context menu
                ["menu_connect"] = "▶️ Connect",
                ["menu_disconnect"] = "⏹️ Disconnect",
                ["menu_add_favorite"] = "⭐ Add to Favorites",
                ["menu_remove_favorite"] = "❌ Remove from Favorites",
                ["menu_remove_server"] = "🗑️ Remove server",
                ["menu_properties"] = "📋 Properties",
                ["menu_rename"] = "✏️ Rename",
                ["driver_installed"] = "Installed ✓",
                ["driver_not_instantiated"] = "Not instantiated",
                ["driver_not_installed"] = "Not installed",
                ["mode_zerocopy"] = "⚡ Mode: Zero-Copy",
                ["mode_lowlatency"] = "🏎️ Mode: Low-Latency",

                // Update
                ["update_checking"] = "Checking for updates...",
                ["update_available"] = "New version available",
                ["update_current"] = "You have the latest version",
                ["update_download"] = "Download and install",
                ["update_error"] = "Error checking for updates",
                ["update_downloading"] = "Downloading update...",
                ["msg_update_title"] = "Update Available",
                ["msg_update_prompt"] = "New version available: v{0}\n\n{1}\n\nDo you want to download and install the update?",

                // Device Properties & Rename Dialogs
                ["props_title"] = "Device Properties",
                ["props_busid"] = "Bus ID",
                ["props_server"] = "Server",
                ["props_desc"] = "Description",
                ["props_vidpid"] = "VID:PID",
                ["props_vendor"] = "Vendor",
                ["props_product"] = "Product",
                ["rename_title"] = "Rename Device",
                ["rename_prompt"] = "Rename device {0}:{1}\n\nNew name:",

                // Dialogs
                ["enter_ip"] = "Enter a server IP",
                ["enter_ip_ssh"] = "Enter the server IP for SSH",
                ["app_already_running"] = "USBAnywhere is already running.\nCheck the system tray.",
                ["app_admin_error"] = "Could not elevate to administrator:\n{0}",

                // SSH Dialog
                ["ssh_title"] = "SSH - Configure USB/IP Server",
                ["ssh_instructions"] = "📋 Instructions for Linux / Raspberry Pi / Armbian:",
                ["ssh_step1"] = "1. Install packages:",
                ["ssh_step2"] = "2. Load kernel module:",
                ["ssh_step3"] = "3. Start service:",
                ["ssh_step4"] = "4. List available USB devices:",
                ["ssh_step5"] = "5. Export device (e.g.: 1-1.4):",
                ["ssh_step6"] = "6. Auto-start (or use the 1-Click Auto-Setup button):",
                ["ssh_user"] = "User:",
                ["ssh_connect"] = "🔗 Connect",
                ["ssh_copy"] = "📋 Copy command",
                ["ssh_copied"] = "✓ Copied!",
                ["ssh_auto_setup"] = "⚡ 1-Click Auto-Setup",
                ["ssh_close"] = "Close",
                ["ssh_invalid_chars"] = "Invalid characters detected in username or IP.",
                ["ssh_security_title"] = "Security Warning",

                // VPN Dialog
                ["vpn_title"] = "VPN / Remote Network",
                ["vpn_status"] = "Network status:",
                ["vpn_checking"] = "Checking...",
                ["vpn_online"] = "Online",
                ["vpn_offline"] = "Offline",
                ["vpn_peers"] = "Peers on network:",
                ["vpn_no_peers"] = "No peers online",
                ["vpn_add_all"] = "Add All",
                ["vpn_connect"] = "Connect",
                ["vpn_info"] = "ℹ️ Connect with your remote servers via Tailscale or ZeroTier",

                // System Tray
                ["tray_scan"] = "Scan network",
                ["tray_show"] = "Show USBAnywhere",
                ["tray_exit"] = "Exit",
                ["tray_minimized"] = "Minimized to system tray",
                ["notify_connected"] = "Device connected",
                ["notify_connection_lost"] = "Connection lost",
                ["notify_reconnected"] = "Reconnection successful",

                // Favorites
                ["log_favorite_added"] = "Device added to favorites",
                ["log_favorite_removed"] = "Device removed from favorites",

                // Activity Log Messages
                ["log_tray_init_error"] = "Could not initialize tray icon: {0}",
                ["log_scanning_subnet"] = "Scanning subnet {0}.0/24...",
                ["log_scan_complete"] = "Scan complete: found {0} server(s)",
                ["log_server_found"] = "  → Server: {0}",
                ["log_devices_on_server"] = "{0} device(s) on {1}",
                ["log_devices_already_connected"] = "Found {0} device(s) already connected",
                ["log_load_connected_error"] = "Could not load connected devices: {0}",
                ["log_auto_connecting"] = "Auto-connecting {0} favorite(s)...",
                ["log_connected_favorite"] = "Connected {0} ({1})",
                ["log_connection_failed"] = "Failed to connect {0}: {1}",
                ["log_connection_error"] = "Error connecting {0}: {1}",
                ["log_connecting_device"] = "Connecting {0} on {1}...",
                ["log_connection_success"] = "Connected {0}",
                ["log_connection_general_failed"] = "Connection failed",
                ["log_disconnecting"] = "Disconnecting {0}...",
                ["log_disconnected_port"] = "Disconnected port {0}",
                ["log_disconnect_failed"] = "Disconnect failed",
                ["log_device_not_found"] = "Device not found, may already be disconnected",
                ["log_listing_devices"] = "Listing devices on {0} server(s)...",
                ["log_listed_devices"] = "Listed {0} devices on {1} server(s)",
                ["log_checking_updates"] = "Checking for updates...",
                ["log_update_available"] = "New version available: {0}",
                ["log_downloading_update"] = "Downloading update...",
                ["log_update_failed"] = "Update failed",
                ["log_latest_version"] = "You have the latest version (v{0})",
                ["log_installing_drivers"] = "Installing drivers...",
                ["log_drivers_installed"] = "Drivers installed successfully",
                ["log_install_drivers_failed"] = "Failed to install drivers",
                ["log_uninstalling_drivers"] = "Uninstalling drivers...",
                ["log_drivers_uninstalled"] = "Drivers uninstalled successfully",
                ["log_uninstall_drivers_failed"] = "Failed to uninstall drivers",
            }
        };
    }

    public string GetText(string key, string language = "es")
    {
        if (_translations.TryGetValue(language, out var dict) && dict.TryGetValue(key, out var text))
        {
            return text;
        }
        if (_translations.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enText))
        {
            return enText;
        }
        return key;
    }
}
