using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
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
    private static readonly HashSet<string> KnownDevnodeHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        "5B9C45F5BE344676BB434C14697965DE608FC3B0F6CAE7E5670B1D90C9196806", // v0.9.7.x x64
        "CCBF1813C7EF33303A7FEBE6E288F27BFA9B0EA189D1BAAFCBABF2ADA7105964", // v0.9.8.1 x64
        "686F7D053599DBC7E8DE0ED2AA215B2218538FADD923A2985FFB72F452F0B8B1", // v0.9.8.1 arm64
    };
    private const string UdeHardwareId = "ROOT\\USBIP_WIN2\\UDE";
    private static readonly string PnputilPath = GetSystemPnputilPath();

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
                        return new DriverStatus { Installed = true, Status = "INSTALADO" };
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

            // 1. Install filter driver first if present
            var filterInf = Path.Combine(driverDir, "usbip2_filter.inf");
            if (File.Exists(filterInf))
            {
                progress?.Report("Instalando filtro de drivers USB/IP (usbip2_filter)...");
                _logger.LogInformation("Instalando driver de filtro: {Inf}", filterInf);
                var filterRes = await RunProcessAsync(PnputilPath, $"/add-driver \"{filterInf}\" /install");
                if (!filterRes.Success && filterRes.Error != "Reinicio requerido")
                {
                    _logger.LogWarning("Aviso en usbip2_filter: {Err}", filterRes.Error);
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
                // Fallback for any other .inf in directory
                var otherInfs = Directory.GetFiles(driverDir, "*.inf", SearchOption.AllDirectories);
                foreach (var inf in otherInfs)
                {
                    progress?.Report($"Instalando: {Path.GetFileName(inf)}...");
                    var res = await RunProcessAsync(PnputilPath, $"/add-driver \"{inf}\" /install");
                    if (!res.Success && res.Error != "Reinicio requerido") return res;
                }
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

            // 2. Enumerate installed driver packages from DriverStore
            progress?.Report("Enumerando drivers en DriverStore...");
            var psi = new ProcessStartInfo
            {
                FileName = PnputilPath,
                Arguments = "/enum-drivers",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var enumProcess = Process.Start(psi);
            if (enumProcess == null)
            {
                _logger.LogError("Error al iniciar enumeración de drivers.");
                return new OperationResult { Success = false, Error = "Error al enumerar drivers." };
            }

            var output = await enumProcess.StandardOutput.ReadToEndAsync();
            await enumProcess.WaitForExitAsync();

            var usbipDrivers = ExtractPublishedNames(output);

            if (!usbipDrivers.Any())
            {
                _logger.LogInformation("No se encontraron drivers USBIP para remover en DriverStore.");
                progress?.Report("No hay drivers de USBIP para remover.");
                return new OperationResult { Success = true };
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

            _logger.LogInformation("Secuencia de desinstalación finalizada.");
            progress?.Report("Drivers removidos.");
            return new OperationResult { Success = true };
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

            return new OperationResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando {File} {Args}", fileName, arguments);
            return new OperationResult { Success = false, Error = ex.Message };
        }
    }
}
