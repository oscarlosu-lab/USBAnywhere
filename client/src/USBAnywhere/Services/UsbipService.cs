using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using USBAnywhere.Models;
using USBAnywhere.Services.Interfaces;
using USBAnywhere.Utils;

namespace USBAnywhere.Services
{
    /// <summary>
    /// Service for USBIP operations with input validation, binary integrity checks and robust output parsing.
    /// </summary>
    public class UsbipService : IUsbipService
    {
        private readonly ILogger<UsbipService> _logger;
        private readonly IEnvironmentService _envService;
        private readonly IDriverService _driverService;

        // Integrity check: SHA-256 of the bundled usbip-win client binaries, Portable/usbipw.exe == usbip.exe)
        // Any binary not listed here is NOT executed (fail-closed). Update when shipping a new usbip build.
        internal static readonly HashSet<string> ValidHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "817DA219FA153B4B37C3BC1AA8FCC63F33E046188C57CC3DD2C60A6EF0883C56", // usbipw.exe / usbip.exe (x64 bundled)
            "90663093D9E13795E567536328EC960D2AB5275F6342C5D3342CDF13E397E579"  // usbipw.exe / usbip.exe (ARM64 bundled)
        };

        // Whitelist for companion native DLLs loaded in the usbip directory (anti-DLL planting)
        internal static readonly HashSet<string> ValidDllHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "F123EF5D288B817A890CBE39A3C2D212B3D8B1765C691C8703CCB76C7337986C", // libusbip.dll (x64)
            "593AE7551B53FF34E8EB6FA25CF8E00523606A9F9F8129F83C8EB94195A44F0B", // libusbip.dll (ARM64)
            "4BFC862E5891D9B4C12A8EBD43D3BA871EBF94B65F7A00C22527C905949E0FFF", // resources.dll (x64)
            "B9FF5A60212F7BDED02A16510EEE031C59C312A4A396AC2956478E6A32165928"  // resources.dll (ARM64)
        };

        public UsbipService(
            ILogger<UsbipService> logger,
            IEnvironmentService envService,
            IDriverService driverService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _envService = envService ?? throw new ArgumentNullException(nameof(envService));
            _driverService = driverService ?? throw new ArgumentNullException(nameof(driverService));
        }

        public async Task<List<UsbDevice>> ListRemoteDevicesAsync(string ip, CancellationToken ct = default)
        {
            _logger.LogInformation("Escaneando dispositivos remotos en {IP}...", ip);
            var devices = new List<UsbDevice>();
            
            if (!InputValidator.IsValidHost(ip))
            {
                _logger.LogWarning("SEGURIDAD: IP/host inválido rechazado: {IP}", ip);
                return devices;
            }

            try
            {
                var result = await RunCommandAsync(new[] { "list", "-r", ip }, 15000, ct);
                if (string.IsNullOrEmpty(result.Output)) return devices;

                // Pattern 1: Modern usbip / usbipw format:
                //   3-1    : Realtek Semiconductor Corp. : unknown product (0bda:5100)
                var regexModern = new Regex(@"^\s+([\d\-\.]+)\s*:\s*(.+?)\s*\(([0-9a-fA-F]{4}:[0-9a-fA-F]{4})\)\s*$", RegexOptions.Multiline);
                var matchesModern = regexModern.Matches(result.Output);

                if (matchesModern.Count > 0)
                {
                    foreach (Match match in matchesModern)
                    {
                        devices.Add(new UsbDevice
                        {
                            BusId = match.Groups[1].Value.Trim(),
                            Description = match.Groups[2].Value.Trim(),
                            HardwareId = match.Groups[3].Value.Trim(),
                            RemoteIp = ip,
                            ServerIP = ip,
                            IsConnected = false
                        });
                    }
                }
                else
                {
                    // Pattern 2: Legacy usbip format:
                    //   3-1    : 0bda:5100 Realtek Semiconductor Corp.
                    var regexLegacy = new Regex(@"^\s+([\d\-\.]+)\s*:\s*([0-9a-fA-F]{4}:[0-9a-fA-F]{4})\s+(.+)$", RegexOptions.Multiline);
                    var matchesLegacy = regexLegacy.Matches(result.Output);

                    foreach (Match match in matchesLegacy)
                    {
                        devices.Add(new UsbDevice
                        {
                            BusId = match.Groups[1].Value.Trim(),
                            HardwareId = match.Groups[2].Value.Trim(),
                            Description = match.Groups[3].Value.Trim(),
                            RemoteIp = ip,
                            ServerIP = ip,
                            IsConnected = false
                        });
                    }
                }

                // Pattern 3: Fallback line-by-line if still empty
                if (devices.Count == 0)
                {
                    var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.Trim();
                        if (line.StartsWith("=") || line.StartsWith("/") || line.StartsWith("Exportable", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var headerMatch = Regex.Match(line, @"^([\d\-\.]+)\s*:\s*(.+)$");
                        if (headerMatch.Success)
                        {
                            var busId = headerMatch.Groups[1].Value.Trim();
                            var rest = headerMatch.Groups[2].Value.Trim();

                            if (Regex.IsMatch(busId, @"^\d+$") && rest.Contains("Interface", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var vidPidMatch = Regex.Match(rest, @"\b([0-9a-fA-F]{4}:[0-9a-fA-F]{4})\b");
                            string hardwareId = vidPidMatch.Success ? vidPidMatch.Groups[1].Value : string.Empty;

                            string desc = rest;
                            if (vidPidMatch.Success)
                            {
                                desc = Regex.Replace(desc, @"\s*\(" + Regex.Escape(hardwareId) + @"\)", "").Trim();
                                desc = Regex.Replace(desc, @"\b" + Regex.Escape(hardwareId) + @"\b", "").Trim();
                            }
                            if (string.IsNullOrWhiteSpace(desc)) desc = "USB Device";

                            devices.Add(new UsbDevice
                            {
                                BusId = busId,
                                HardwareId = hardwareId,
                                Description = desc,
                                RemoteIp = ip,
                                ServerIP = ip,
                                IsConnected = false
                            });
                        }
                    }
                }

                _logger.LogInformation("Encontrados {Count} dispositivos en {IP}", devices.Count, ip);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico al listar dispositivos remotos en {IP}", ip);
            }

            return devices;
        }

        public async Task<bool> AttachDeviceAsync(string ip, string busId, string? receiveMode = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(busId)) return false;

            if (!InputValidator.IsValidHost(ip) || !InputValidator.IsValidBusId(busId))
            {
                _logger.LogWarning("SEGURIDAD: Parámetros inválidos rechazados (ip='{IP}', busId='{BusId}')", ip, busId);
                return false;
            }

            _logger.LogInformation("Intentando conectar dispositivo {BusId} desde {IP} (modo: {Mode})...", busId, ip, receiveMode ?? "default");
            
            try
            {
                if (!await _driverService.IsDriverInstalledAsync())
                {
                    _logger.LogWarning("Driver usbip-win no detectado. La conexión podría fallar.");
                }

                var cmdArgs = new List<string> { "attach", "-r", ip, "-b", busId };
                if (!string.IsNullOrWhiteSpace(receiveMode))
                {
                    var normalized = receiveMode.Trim().ToLowerInvariant();
                    if (normalized == "zero-copy" || normalized == "low-latency")
                    {
                        cmdArgs.Add($"--receive-mode={normalized}");
                    }
                }

                var result = await RunCommandAsync(cmdArgs, 10000, ct);
                
                // FIX: usbip attach returns exit code 0 on success (output may be empty)
                // Only fail if exit code != 0 OR output explicitly contains error keywords
                bool hasError = result.Output.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                                result.Output.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                                result.Error.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                                result.Error.Contains("failed", StringComparison.OrdinalIgnoreCase);
                
                bool success = result.ExitCode == 0 && !hasError;
                
                if (success) _logger.LogInformation("Dispositivo {BusId} conectado exitosamente.", busId);
                else _logger.LogError("Fallo al conectar {BusId}. ExitCode: {ExitCode}, Output: {Output}, Error: {Error}", 
                    busId, result.ExitCode, result.Output, result.Error);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al conectar dispositivo {BusId}", busId);
                return false;
            }
        }

        public async Task<bool> DetachDeviceAsync(string port, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(port)) return false;

            if (!InputValidator.IsValidPort(port))
            {
                _logger.LogWarning("SEGURIDAD: Puerto inválido rechazado: '{Port}'", port);
                return false;
            }

            _logger.LogInformation("Desconectando dispositivo en puerto {Port}...", port);

            try
            {
                var result = await RunCommandAsync(new[] { "detach", "-p", port }, 5000, ct);
                
                // FIX: usbip detach produces empty output on success — use exit code
                bool hasError = result.Output.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                                result.Error.Contains("error", StringComparison.OrdinalIgnoreCase);
                
                bool success = result.ExitCode == 0 && !hasError;
                
                if (success) _logger.LogInformation("Puerto {Port} liberado.", port);
                else _logger.LogError("Fallo al desconectar puerto {Port}. ExitCode: {ExitCode}, Error: {Error}", 
                    port, result.ExitCode, result.Error);
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desconectar puerto {Port}", port);
                return false;
            }
        }

        /// <summary>
        /// Result of a command execution including exit code
        /// </summary>
        private record CommandResult(string Output, string Error, int ExitCode);

        private async Task<CommandResult> RunCommandAsync(IReadOnlyList<string> args, int timeoutMs, CancellationToken ct)
        {
            var exePath = GetUsbipExecutable();
            if (exePath == null)
            {
                _logger.LogError("Binario USBIP no encontrado en {Path}", _envService.BinPath);
                return new CommandResult(string.Empty, "Binary not found", -1);
            }

            // Security Gate
            if (!ValidateBinaryHash(exePath))
            {
                _logger.LogCritical("SEGURIDAD: El binario {Path} no es de confianza. Operación abortada.", exePath);
                return new CommandResult(string.Empty, "Untrusted binary", -2);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? _envService.BinPath,
                StandardOutputEncoding = Encoding.UTF8
            };
            // ArgumentList: each token is quoted by the runtime → no argument injection
            foreach (var a in args) startInfo.ArgumentList.Add(a);

            using (var process = new Process { StartInfo = startInfo })
            {
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (outputBuilder) outputBuilder.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (errorBuilder) errorBuilder.AppendLine(e.Data); };

                _logger.LogDebug("Exec: {Exe} {Args}", exePath, string.Join(' ', args));
                
                if (!process.Start()) return new CommandResult(string.Empty, "Failed to start process", -3);

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                int exitCode = -1;
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    var waitTask = Task.Run(() => process.WaitForExit(), cts.Token);
                    if (await Task.WhenAny(waitTask, Task.Delay(timeoutMs, cts.Token)) == waitTask)
                    {
                        exitCode = process.ExitCode;
                    }
                    else
                    {
                        _logger.LogWarning("Comando USBIP excedió el timeout de {Timeout}ms.", timeoutMs);
                        try 
                        { 
                            process.Kill(entireProcessTree: true); 
                            process.WaitForExit(2000);
                        } 
                        catch (Exception kex) 
                        { 
                            _logger.LogDebug(kex, "No se pudo terminar el árbol de procesos usbip tras timeout"); 
                        }
                        exitCode = -4;
                    }
                }

                string errorResult; lock (errorBuilder) errorResult = errorBuilder.ToString();
                if (!string.IsNullOrEmpty(errorResult))
                {
                    _logger.LogWarning("USBIP Stderr: {Error}", errorResult);
                }

                string outputResult; lock (outputBuilder) outputResult = outputBuilder.ToString(); return new CommandResult(outputResult, errorResult, exitCode);
            }
        }

        private string? GetUsbipExecutable()
        {
            var candidates = new[] { "usbipw.exe", "usbip-win.exe", "usbip.exe" };
            
            // Search in BinPath first
            foreach (var name in candidates)
            {
                var path = Path.Combine(_envService.BinPath, name);
                if (File.Exists(path)) return path;
            }
            
            // FIX: Also search in BaseDirectory (portable mode — binaries next to exe)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var name in candidates)
            {
                var path = Path.Combine(baseDir, name);
                if (File.Exists(path)) return path;
            }
            
            return null;
        }

        private bool ValidateBinaryHash(string path)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                {
                    // 1. Validate the executable itself
                    string exeHash;
                    using (var stream = File.OpenRead(path))
                    {
                        var hashBytes = sha256.ComputeHash(stream);
                        exeHash = BitConverter.ToString(hashBytes).Replace("-", "");
                    }

                    if (!ValidHashes.Contains(exeHash))
                    {
                        _logger.LogCritical("SEGURIDAD: Hash de ejecutable NO reconocido: {Hash} ({Path}). Ejecución bloqueada.", exeHash, path);
                        return false;
                    }

                    // 2. Validate companion DLLs in the executable directory against DLL planting (F-04)
                    var binDir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(binDir) && Directory.Exists(binDir))
                    {
                        var dllFiles = Directory.GetFiles(binDir, "*.dll");
                        foreach (var dll in dllFiles)
                        {
                            using var dllStream = File.OpenRead(dll);
                            var dllBytes = sha256.ComputeHash(dllStream);
                            var dllHash = BitConverter.ToString(dllBytes).Replace("-", "");

                            if (!ValidDllHashes.Contains(dllHash))
                            {
                                _logger.LogCritical("SEGURIDAD: DLL no autorizada o manipulada detectada: {Dll} (Hash: {Hash}). Operación abortada.", dll, dllHash);
                                return false;
                            }
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo en la validación de integridad de {Path}", path);
                return false;
            }
        }

        // ── IUsbipService compatibility methods ──────────────────────────────

        public Task<List<UsbDevice>> ListDevicesAsync(string serverIP)
            => ListRemoteDevicesAsync(serverIP);

        public async Task<OperationResult> ConnectAsync(string serverIP, string busId, string? receiveMode = null)
        {
            bool ok = await AttachDeviceAsync(serverIP, busId, receiveMode);
            return new OperationResult { Success = ok, Error = ok ? null : "Attach failed" };
        }

        public async Task<OperationResult> DisconnectAsync(int port)
        {
            bool ok = await DetachDeviceAsync(port.ToString());
            return new OperationResult { Success = ok, Error = ok ? null : "Detach failed" };
        }

        public async Task<int> DetachAllConnectedDevicesAsync(CancellationToken ct = default)
        {
            int detached = 0;
            try
            {
                _logger.LogInformation("Iniciando desconexión limpia de todos los dispositivos...");
                var connected = await GetConnectedDevicesAsync();
                foreach (var dev in connected)
                {
                    if (await DetachDeviceAsync(dev.Port.ToString(), ct))
                    {
                        detached++;
                    }
                }
                _logger.LogInformation("Desconexión limpia finalizada. Total liberados: {Count}", detached);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Aviso durante desconexión masiva de dispositivos");
            }
            return detached;
        }

        /// <summary>
        /// FIX: Actually parse 'usbip port' output to detect connected devices.
        /// This enables connection monitoring, auto-reconnect, and disconnect by port.
        /// </summary>
        public async Task<List<ConnectedDevice>> GetConnectedDevicesAsync()
        {
            var devices = new List<ConnectedDevice>();
            
            try
            {
                var result = await RunCommandAsync(new[] { "port" }, 10000, CancellationToken.None);
                if (string.IsNullOrEmpty(result.Output)) return devices;
                
                // Parse usbip port output format:
                // Port 00: <Port in Use> at Speed(480Mbps)
                //        Server IP: 192.168.1.132
                //        busid: 1-1.4 (0bda:5100)
                //  or (usbip-win format):
                // Port 0: imported
                //     remote: 192.168.1.132
                //     bus id: 1-1.4 (class: 0e / subclass: 01 / protocol: 00)
                
                var lines = result.Output.Split('\n', StringSplitOptions.None);
                
                int currentPort = -1;
                string currentServerIP = "";
                string currentBusId = "";
                string currentVidPid = "";
                string currentDesc = "";
                bool portInUse = false;
                
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    
                    // Match port line: "Port XX:" or "Port 0:"
                    var portMatch = Regex.Match(line, @"Port\s+(\d+)\s*:", RegexOptions.IgnoreCase);
                    if (portMatch.Success)
                    {
                        // Save previous device if valid
                        if (portInUse && currentPort >= 0 && !string.IsNullOrEmpty(currentServerIP))
                        {
                            devices.Add(new ConnectedDevice
                            {
                                Port = currentPort,
                                ServerIP = currentServerIP,
                                BusId = currentBusId,
                                VendorId = currentVidPid.Contains(':') ? currentVidPid.Split(':')[0] : "",
                                ProductId = currentVidPid.Contains(':') ? currentVidPid.Split(':')[1] : "",
                                Description = currentDesc
                            });
                        }
                        
                        currentPort = int.Parse(portMatch.Groups[1].Value);
                        currentServerIP = "";
                        currentBusId = "";
                        currentVidPid = "";
                        currentDesc = "";
                        
                        // Check if port is in use
                        portInUse = line.Contains("Use", StringComparison.OrdinalIgnoreCase) || 
                                    line.Contains("imported", StringComparison.OrdinalIgnoreCase) ||
                                    !line.Contains("empty", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    
                    // Match modern usbip URI: "-> usbip://192.168.1.162:3240/5-1"
                    var urlMatch = Regex.Match(line, @"usbip://([a-zA-Z0-9\.\-]+)(?::\d+)?/([\d\-\.]+)", RegexOptions.IgnoreCase);
                    if (urlMatch.Success)
                    {
                        currentServerIP = urlMatch.Groups[1].Value;
                        currentBusId = urlMatch.Groups[2].Value;
                        continue;
                    }

                    // Match modern description line: "QinHeng Electronics : CH340 serial converter (1a86:7523)"
                    if (string.IsNullOrEmpty(currentVidPid) && !line.StartsWith("->"))
                    {
                        var descMatch = Regex.Match(line, @"^([^\->\r\n]+?)\s*\(([0-9a-fA-F]{4}:[0-9a-fA-F]{4})\)");
                        if (descMatch.Success)
                        {
                            currentDesc = descMatch.Groups[1].Value.Trim();
                            currentVidPid = descMatch.Groups[2].Value.Trim();
                            continue;
                        }
                    }

                    // Match server IP (legacy / alt format)
                    var serverMatch = Regex.Match(line, @"(?:Server\s+IP|remote)\s*:\s*([a-zA-Z0-9\.\-]+)", RegexOptions.IgnoreCase);
                    if (serverMatch.Success)
                    {
                        currentServerIP = serverMatch.Groups[1].Value;
                        continue;
                    }
                    
                    // Match bus id with VID:PID (legacy format)
                    var busMatch = Regex.Match(line, @"bus\s*id\s*:\s*([\d\-\.]+)\s*(?:\(([0-9a-fA-F]{4}:[0-9a-fA-F]{4})\))?", RegexOptions.IgnoreCase);
                    if (busMatch.Success)
                    {
                        currentBusId = busMatch.Groups[1].Value;
                        if (busMatch.Groups[2].Success)
                            currentVidPid = busMatch.Groups[2].Value;
                        continue;
                    }
                    
                    // Alternate busid format
                    var busMatch2 = Regex.Match(line, @"busid\s*:\s*([\d\-\.]+)\s*\(([0-9a-fA-F]{4}:[0-9a-fA-F]{4})\)", RegexOptions.IgnoreCase);
                    if (busMatch2.Success)
                    {
                        currentBusId = busMatch2.Groups[1].Value;
                        currentVidPid = busMatch2.Groups[2].Value;
                        continue;
                    }
                }
                
                // Don't forget last device
                if (portInUse && currentPort >= 0 && !string.IsNullOrEmpty(currentServerIP))
                {
                    devices.Add(new ConnectedDevice
                    {
                        Port = currentPort,
                        ServerIP = currentServerIP,
                        BusId = currentBusId,
                        VendorId = currentVidPid.Contains(':') ? currentVidPid.Split(':')[0] : "",
                        ProductId = currentVidPid.Contains(':') ? currentVidPid.Split(':')[1] : "",
                        Description = currentDesc
                    });
                }
                
                _logger.LogDebug("GetConnectedDevicesAsync found {Count} connected device(s)", devices.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing connected devices from 'usbip port'");
            }
            
            return devices;
        }
    }
}