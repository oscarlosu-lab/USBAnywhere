using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using USBAnywhere.Models;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// Industrial Driver Service - Manage USBIP drivers via Plug-and-Play Utility (pnputil)
/// and virtual device node creator (devnode.exe).
/// </summary>
public class DriverService : IDriverService
{
    public static readonly HashSet<string> KnownDevnodeHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        "5B9C45F5BE344676BB434C14697965DE608FC3B0F6CAE7E5670B1D90C9196806", // v0.9.7.x x64
        "CCBF1813C7EF33303A7FEBE6E288F27BFA9B0EA189D1BAAFCBABF2ADA7105964", // v0.9.8.1 x64
        "686F7D053599DBC7E8DE0ED2AA215B2218538FADD923A2985FFB72F452F0B8B1", // v0.9.8.1 arm64
    };
    private const string UdeHardwareId = "ROOT\\USBIP_WIN2\\UDE";
    private const string UsbClassRegistryPath = @"SYSTEM\CurrentControlSet\Control\Class\{36fc9e60-c465-11cf-8056-444553540000}";
    private const string FilterServiceRegistryPath = @"SYSTEM\CurrentControlSet\Services\usbip2_filter";
    private const string UsbRootHub30EnumPath = @"SYSTEM\CurrentControlSet\Enum\USB\ROOT_HUB30";
    private const string FilterDriverName = "usbip2_filter";
    private static readonly string PnputilPath = GetSystemPnputilPath();

    /// <summary>
    /// Pure helper to strip usbip2_filter from a Registry REG_MULTI_SZ UpperFilters array
    /// while preserving all other system filter drivers (e.g. TsUsbFlt, usbfilter).
    /// Prevents loss of local USB input devices or INACCESSIBLE_BOOT_DEVICE on driver removal
    /// (upstream vadimgrn/usbip-win2 issues #155 and #201).
    /// </summary>
    public static string[] RemoveFilterFromMultiString(string[]? currentFilters, string filterToRemove = FilterDriverName)
    {
        if (currentFilters == null || currentFilters.Length == 0)
            return Array.Empty<string>();

        return currentFilters
            .Where(f => !string.IsNullOrWhiteSpace(f) &&
                        !string.Equals(f.Trim(), filterToRemove, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// Detects if Windows is running as Windows To Go / Portable USB-booted OS.
    /// Installing usbip2_filter (StartType=3 DEMAND_START, ErrorControl=2 SEVERE on USB\ROOT_HUB30)
    /// on a USB-booted Windows installation causes INACCESSIBLE_BOOT_DEVICE at next boot (issue #201).
    /// </summary>
    public static bool IsPortableOperatingSystem()
    {
        try
        {
            using var controlKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control", writable: false);
            if (controlKey?.GetValue("PortableOperatingSystem") is int portableVal && portableVal != 0)
            {
                return true;
            }
        }
        catch
        {
            // Non-fatal if registry read is restricted
        }
        return false;
    }

    private void SanitizeUsbFilterBeforeUninstall()
    {
        try
        {
            // 1. Disarm SERVICE_ERROR_SEVERE (2) -> SERVICE_ERROR_IGNORE (0) on usbip2_filter service
            using (var svcKey = Registry.LocalMachine.OpenSubKey(FilterServiceRegistryPath, writable: true))
            {
                if (svcKey != null)
                {
                    svcKey.SetValue("ErrorControl", 0, RegistryValueKind.DWord);
                    _logger.LogInformation("Desarmado ErrorControl=0 en servicio usbip2_filter antes de desinstalar.");
                }
            }

            // 2. Clean UpperFilters in USB Class key {36fc9e60-c465-11cf-8056-444553540000}
            using (var usbClassKey = Registry.LocalMachine.OpenSubKey(UsbClassRegistryPath, writable: true))
            {
                if (usbClassKey?.GetValue("UpperFilters") is string[] upperFilters)
                {
                    var cleaned = RemoveFilterFromMultiString(upperFilters, FilterDriverName);
                    if (cleaned.Length != upperFilters.Length)
                    {
                        if (cleaned.Length == 0)
                            usbClassKey.DeleteValue("UpperFilters", throwOnMissingValue: false);
                        else
                            usbClassKey.SetValue("UpperFilters", cleaned, RegistryValueKind.MultiString);

                        _logger.LogInformation("Limpiada entrada {Filter} de USB Class UpperFilters.", FilterDriverName);
                    }
                }
            }

            // 3. Clean UpperFilters on Enum\USB\ROOT_HUB30 instances if orphaned
            using (var rootHubKey = Registry.LocalMachine.OpenSubKey(UsbRootHub30EnumPath, writable: false))
            {
                if (rootHubKey != null)
                {
                    foreach (var subKeyName in rootHubKey.GetSubKeyNames())
                    {
                        try
                        {
                            using var instKey = rootHubKey.OpenSubKey(subKeyName, writable: true);
                            if (instKey?.GetValue("UpperFilters") is string[] instFilters)
                            {
                                var cleaned = RemoveFilterFromMultiString(instFilters, FilterDriverName);
                                if (cleaned.Length != instFilters.Length)
                                {
                                    if (cleaned.Length == 0)
                                        instKey.DeleteValue("UpperFilters", throwOnMissingValue: false);
                                    else
                                        instKey.SetValue("UpperFilters", cleaned, RegistryValueKind.MultiString);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Aviso no fatal al sanitizar registro de filtros USB antes de desinstalar.");
        }
    }

    private static string GetSystemPnputilPath()
    {
        var sysPath = Path.Combine(Environment.SystemDirectory, "pnputil.exe");
        return File.Exists(sysPath) ? sysPath : "pnputil";
    }

    private readonly IEnvironmentService _envService;
    private readonly ILogger<DriverService> _logger;

    public DriverService(IEnvironmentService envService, ILogger<DriverService> logger)
    {
        _envService = envService;
        _logger = logger;
    }

    public string GetDriverStatus()
    {
        var status = CheckInternalStatus();
        return status.Status;
    }

    public async Task<bool> IsDriverInstalledAsync()
    {
        return await Task.Run(() => CheckInternalStatus().Installed);
    }

    public async Task<bool> InstallDriverAsync()
    {
        var result = await InstallInternalAsync();
        return result.Success;
    }

    public async Task<bool> UninstallDriverAsync()
    {
        var result = await UninstallInternalAsync();
        return result.Success;
    }

    private DriverStatus CheckInternalStatus()
    {
        try
        {
            _logger.LogInformation("Verificando estado activo del driver USB/IP...");

            // 1. Check active connected USB devices: look for USBip 3.X Emulated Host Controller or ROOT\USBIP_WIN2
            var devInfo = new ProcessStartInfo
            {
                FileName = PnputilPath,
                Arguments = "/enum-devices /class USB /connected",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var devProc = Process.Start(devInfo);
            if (devProc != null)
            {
                var devTask = devProc.StandardOutput.ReadToEndAsync();
                if (devProc.WaitForExit(10000))
                {
                    var devOut = devTask.GetAwaiter().GetResult();
                    bool isDeviceActive = Regex.IsMatch(devOut, @"USBip\s+3\.X\s+Emulated\s+Host\s+Controller|ROOT\\USBIP_WIN2", RegexOptions.IgnoreCase);
                    if (isDeviceActive)
                    {
                        _logger.LogDebug("Dispositivo emulador USB/IP detectado y activo en el árbol PnP.");
                        return new DriverStatus { Installed = true, Status = "INSTALADO" };
                    }
                }
                else
                {
                    try { devProc.Kill(true); } catch { }
                }
            }

            // 2. Check DriverStore: look if driver package is at least staged
            var drvInfo = new ProcessStartInfo
            {
                FileName = PnputilPath,
                Arguments = "/enum-drivers",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var drvProc = Process.Start(drvInfo);
            if (drvProc != null)
            {
                var drvTask = drvProc.StandardOutput.ReadToEndAsync();
                if (drvProc.WaitForExit(10000))
                {
                    var drvOut = drvTask.GetAwaiter().GetResult();
                    bool isStaged = Regex.IsMatch(drvOut, @"usbip2_ude|usbip_vhci|usbip-win|USBIP-WIN2", RegexOptions.IgnoreCase);
                    if (isStaged)
                    {
                        _logger.LogWarning("Drivers presentes en DriverStore pero dispositivo UDE no instanciado.");
                        return new DriverStatus { Installed = false, Status = "NO_INSTANCIADO" };
                    }
                }
                else
                {
                    try { drvProc.Kill(true); } catch { }
                }
            }

            return new DriverStatus { Installed = false, Status = "FALTANTE" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CheckInternalStatus");
            return new DriverStatus { Installed = false, Status = $"Error: {ex.Message}" };
        }
    }

    private async Task<OperationResult> InstallInternalAsync(IProgress<string>? progress = null)
    {
        try
        {
            _logger.LogInformation("Iniciando secuencia de instalación de drivers USB/IP...");
            progress?.Report("Iniciando instalación de drivers...");

            var driverDir = _envService.DriversPath;
            if (!Directory.Exists(driverDir))
            {
                _logger.LogError("Directorio de drivers NO detectado: {Dir}", driverDir);
                return new OperationResult { Success = false, Error = "Directorio de drivers no detectado." };
            }

            // 1. Install filter driver first if present (unless running Windows To Go / Portable USB OS — upstream issue #201)
            var filterInf = Path.Combine(driverDir, "usbip2_filter.inf");
            if (File.Exists(filterInf))
            {
                if (IsPortableOperatingSystem())
                {
                    _logger.LogWarning("Detectado Windows To Go / PortableOperatingSystem=1. Se omite usbip2_filter.inf para prevenir BSOD INACCESSIBLE_BOOT_DEVICE (upstream issue #201).");
                    progress?.Report("Omitiendo usbip2_filter en Windows To Go (protección de arranque USB)...");
                }
                else
                {
                    progress?.Report("Instalando filtro de drivers USB/IP (usbip2_filter)...");
                    _logger.LogInformation("Instalando driver de filtro: {Inf}", filterInf);
                    var filterRes = await RunProcessAsync(PnputilPath, $"/add-driver \"{filterInf}\" /install");
                    if (!filterRes.Success && filterRes.Error != "Reinicio requerido")
                    {
                        _logger.LogWarning("Aviso en usbip2_filter: {Err}", filterRes.Error);
                    }
                }
            }

            // 2. Install UDE driver: use devnode.exe if available to instantiate virtual host controller
            var udeInf = Path.Combine(driverDir, "usbip2_ude.inf");
            if (File.Exists(udeInf))
            {
                progress?.Report("Instalando controlador emulado USB/IP (UDE)...");
                _logger.LogInformation("Instalando controlador UDE: {Inf}", udeInf);

                var devnodePath = FindDevnodeExe();
                if (devnodePath != null && ValidateDevnodeHash(devnodePath))
                {
                    // Clean up any previous UDE node first to prevent duplicate virtual host controllers in Device Manager
                    _logger.LogInformation("Limpiando nodos UDE previos antes de instanciar...");
                    await RunProcessAsync(devnodePath, $"remove {UdeHardwareId} root");

                    _logger.LogInformation("Creando nodo de dispositivo UDE con devnode.exe...");
                    var devnodeRes = await RunProcessAsync(devnodePath, $"install \"{udeInf}\" {UdeHardwareId}");
                    if (!devnodeRes.Success)
                    {
                        _logger.LogWarning("devnode.exe retornó código no cero: {Err}. Intentando fallback pnputil...", devnodeRes.Error);
                        await RunProcessAsync(PnputilPath, $"/add-driver \"{udeInf}\" /install");
                    }
                }
                else
                {
                    _logger.LogInformation("devnode.exe no disponible. Usando pnputil /add-driver...");
                    var udeRes = await RunProcessAsync(PnputilPath, $"/add-driver \"{udeInf}\" /install");
                    if (!udeRes.Success && udeRes.Error != "Reinicio requerido")
                    {
                        return udeRes;
                    }
                }
            }
            else
            {
                _logger.LogError("Archivo de controlador requerido no encontrado: {Inf}", udeInf);
                return OperationResult.Fail("Archivo de controlador usbip2_ude.inf no encontrado.");
            }

            _logger.LogInformation("Secuencia de instalación completada exitosamente.");
            progress?.Report("Instalación completada exitosamente.");
            return new OperationResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Excepción fatal durante InstallDriverAsync");
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    private async Task<OperationResult> UninstallInternalAsync(IProgress<string>? progress = null)
    {
        try
        {
            _logger.LogInformation("Iniciando remoción de drivers USB/IP.");
            progress?.Report("Removiendo dispositivos virtuales...");

            // 1. Remove UDE virtual device node first
            var devnodePath = FindDevnodeExe();
            if (devnodePath != null && ValidateDevnodeHash(devnodePath))
            {
                _logger.LogInformation("Removiendo nodo UDE con devnode.exe...");
                await RunProcessAsync(devnodePath, $"remove {UdeHardwareId} root");
            }

            // 1.5. Sanitize USB Class UpperFilters and disarm ErrorControl on usbip2_filter BEFORE pnputil removal
            // to prevent dead local USB ports (#155) or INACCESSIBLE_BOOT_DEVICE (#201)
            SanitizeUsbFilterBeforeUninstall();

            // 2. Enumerate installed driver packages from DriverStore
            progress?.Report("Enumerando drivers en DriverStore...");
            var enumResult = await RunProcessAsync(PnputilPath, "/enum-drivers");
            if (!enumResult.Success)
            {
                _logger.LogError("Error al iniciar enumeración de drivers: {Error}", enumResult.Error);
                return new OperationResult { Success = false, Error = "Error al enumerar drivers." };
            }

            var output = enumResult.Message ?? string.Empty;
            var usbipDrivers = ExtractPublishedNames(output);

            if (!usbipDrivers.Any())
            {
                _logger.LogInformation("No se encontraron drivers USBIP para remover en DriverStore.");
                progress?.Report("No hay drivers de USBIP para remover.");
                return new OperationResult { Success = true, Message = output };
            }

            _logger.LogInformation("Detectados {Count} drivers para remoción: {Drivers}", usbipDrivers.Count, string.Join(", ", usbipDrivers));

            foreach (var driver in usbipDrivers)
            {
                progress?.Report($"Removiendo {driver}...");
                _logger.LogInformation("Ejecutando delete-driver /uninstall para {Driver}", driver);
                var result = await RunProcessAsync(PnputilPath, $"/delete-driver {driver} /uninstall");
                if (!result.Success)
                {
                    _logger.LogInformation("Reintentando delete-driver /force para {Driver}", driver);
                    result = await RunProcessAsync(PnputilPath, $"/delete-driver {driver} /force");
                    if (!result.Success)
                    {
                        _logger.LogWarning("Aviso al remover {Driver}: {Error}", driver, result.Error);
                    }
                }
            }

            // Re-run sanitization after pnputil removal in case pnputil left a stale UpperFilters reference
            SanitizeUsbFilterBeforeUninstall();

            _logger.LogInformation("Secuencia de desinstalación finalizada.");
            progress?.Report("Drivers removidos.");
            return new OperationResult { Success = true, Message = output };
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Excepción fatal durante UninstallDriverAsync");
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }

    private string? FindDevnodeExe()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devnode.exe"),
            Path.Combine(_envService.BinPath, "devnode.exe"),
            Path.Combine(_envService.DriversPath, "devnode.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Portable", "devnode.exe"),
            Path.Combine(_envService.DriversPath, "..", "devnode.exe")
        };

        foreach (var c in candidates)
        {
            try
            {
                var full = Path.GetFullPath(c);
                if (File.Exists(full)) return full;
            }
            catch { }
        }

        return null;
    }

    private bool ValidateDevnodeHash(string path)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hashBytes = sha256.ComputeHash(stream);
            var hash = BitConverter.ToString(hashBytes).Replace("-", "");

            if (KnownDevnodeHashes.Contains(hash))
                return true;

            _logger.LogWarning("Hash de devnode.exe no reconocido ({Hash}). Por seguridad se omitirá su ejecución.", hash);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar hash de devnode.exe");
            return false;
        }
    }

    private List<string> ExtractPublishedNames(string output)
    {
        var names = new List<string>();
        var blocks = Regex.Split(output, @"\r?\n\r?\n");

        foreach (var block in blocks)
        {
            if (block.Contains("usbip", StringComparison.OrdinalIgnoreCase) ||
                block.Contains("vhci", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(block, @"oem\d+\.inf", RegexOptions.IgnoreCase);
                if (match.Success && !names.Contains(match.Value))
                {
                    names.Add(match.Value);
                }
            }
        }
        return names;
    }

    private async Task<OperationResult> RunProcessAsync(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
                return new OperationResult { Success = false, Error = $"Error al iniciar {fileName}" };

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                return new OperationResult { Success = false, Error = $"Timeout ejecutando {fileName}" };
            }

            await Task.WhenAll(outputTask, errorTask);
            var output = outputTask.Result;
            var error = errorTask.Result;

            if (process.ExitCode != 0)
            {
                if (process.ExitCode == 3010)
                {
                    _logger.LogWarning("Operación exitosa, pero se requiere REINICIO (ExitCode 3010).");
                    return new OperationResult { Success = true, Error = "Reinicio requerido" };
                }
                return new OperationResult { Success = false, Error = string.IsNullOrWhiteSpace(error) ? output : error };
            }

            return new OperationResult { Success = true, Message = output };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando {File} {Args}", fileName, arguments);
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }
}
