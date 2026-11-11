using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        private readonly ConcurrentDictionary<int, (string ServerIP, string BusId)> _portBindings = new();

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

        private static readonly string[] RequiredNativeDlls = { "libusbip.dll", "resources.dll" };
        private static readonly string[] HijackCandidateDlls = { "version.dll", "winmm.dll", "dbghelp.dll", "cryptbase.dll", "dwmapi.dll" };

        // High-speed metadata cache so 2-second 'usbip port' polling does not re-read 2.5 MB from disk
        // unless file length, write timestamp, or directory contents change.
        private readonly object _integrityCacheLock = new();
        private string? _cachedIntegritySignature;
        private bool _cachedIntegrityResult;

        /// <summary>
        /// Resolves the optimal usbip2_ude receive mode. Interactive Interrupt IN and Isochronous
        /// peripherals (controllers, mice, keyboards, HID dongles, audio headsets) require 'low-latency'
        /// to avoid Linux/UDE EOVERFLOW (-75) stalls (upstream issue #187).
        /// </summary>
        public static string ResolveEffectiveReceiveMode(string? requestedMode, string? description = null, string? hardwareId = null)
        {
            if (!string.IsNullOrWhiteSpace(requestedMode))
            {
                var norm = requestedMode.Trim().ToLowerInvariant();
                if (norm == "low-latency") return "low-latency";
                if (norm == "zero-copy" && !IsInteractiveOrIsochronousDevice(description, hardwareId))
                {
                    return "zero-copy";
                }
            }

            if (IsInteractiveOrIsochronousDevice(description, hardwareId))
            {
                return "low-latency";
            }

            return "low-latency";
        }

        public static bool IsInteractiveOrIsochronousDevice(string? description, string? hardwareId)
        {
            if (!string.IsNullOrWhiteSpace(hardwareId))
            {
                var vid = hardwareId.Split(':')[0].Trim().ToLowerInvariant();
                // Sony, Microsoft, Nintendo, GameSir, Logitech, Razer, 8BitDo, Thrustmaster, Fanatec, SteelSeries, Corsair, Elgato
                if (vid is "054c" or "045e" or "057e" or "3537" or "046d" or "1532" or "2dc8" or "044f" or "0eb7" or "1038" or "1b1c" or "0fd9")
                {
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                var d = description.ToLowerInvariant();
                string[] interactiveKeywords =
                {
                    "hid", "controller", "gamepad", "dualsense", "dualshock", "wireless controller",
                    "xbox", "gamesir", "8bitdo", "joystick", "wheel", "pedal", "mouse", "keyboard",
                    "receiver", "lightspeed", "dongle", "audio", "headset", "microphone", "dac",
                    "sound", "speaker", "webcam", "camera", "input"
                };
                foreach (var kw in interactiveKeywords)
                {
                    if (d.Contains(kw, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

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
                if (!string.IsNullOrEmpty(result.Output))
                {
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
                } // end if (!string.IsNullOrEmpty(result.Output))

                // Dynamic Control API (TCP 3241) discovery for unbound/local physical devices
                await TryQueryDynamicInventoryAsync(ip, devices, ct);

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
            var result = await AttachDeviceWithResultAsync(ip, busId, receiveMode, ct);
            return result.Success;
        }

        private async Task<OperationResult> AttachDeviceWithResultAsync(string ip, string busId, string? receiveMode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(busId))
            {
                return OperationResult.Fail("Server IP and USB bus ID are required.");
            }

            if (!InputValidator.IsValidHost(ip) || !InputValidator.IsValidBusId(busId))
            {
                _logger.LogWarning("SEGURIDAD: Parámetros inválidos rechazados (ip='{IP}', busId='{BusId}')", ip, busId);
                return OperationResult.Fail("Invalid server IP or USB bus ID.");
            }

            _logger.LogInformation("Intentando conectar dispositivo {BusId} desde {IP} (modo: {Mode})...", busId, ip, receiveMode ?? "default");

            try
            {
                if (!await _driverService.IsDriverInstalledAsync())
                {
                    _logger.LogWarning("Driver usbip-win no detectado. La conexión podría fallar.");
                }

                // Dynamic on-demand binding (USBAnywhere Dynamic Daemon on port 3241)
                var bindStatus = await TryDynamicBindAsync(ip, busId, ct);
                if (!bindStatus.Allowed)
                {
                    var error = bindStatus.ErrorMessage ?? $"Device {busId} blocked by server export policy";
                    _logger.LogWarning("Conexión denegada por política del servidor {IP}: {Reason}", ip, error);
                    return OperationResult.Fail(error);
                }

                if (bindStatus.BoundOnDemand)
                {
                    // Allow usbipd a brief window to register the newly bound device
                    await Task.Delay(150, ct);
                }

                var cmdArgs = new List<string> { "attach", "-r", ip, "-b", busId, "--once" };
                var effectiveMode = string.IsNullOrWhiteSpace(receiveMode)
                    ? "low-latency"
                    : receiveMode.Trim().ToLowerInvariant();
                if (effectiveMode == "zero-copy" || effectiveMode == "low-latency")
                {
                    cmdArgs.Add($"--receive-mode={effectiveMode}");
                }

                var result = await RunCommandAsync(cmdArgs, 10000, ct);

                // FIX: usbip attach returns exit code 0 on success (output may be empty)
                // Only fail if exit code != 0 OR output explicitly contains error keywords
                bool hasError = result.Output.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                                result.Output.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                                result.Error.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                                result.Error.Contains("failed", StringComparison.OrdinalIgnoreCase);

                bool success = result.ExitCode == 0 && !hasError;

                if (!success && bindStatus.BoundOnDemand)
                {
                    await Task.Delay(350, ct);
                    result = await RunCommandAsync(cmdArgs, 10000, ct);
                    hasError = result.Output.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                               result.Output.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                               result.Error.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                               result.Error.Contains("failed", StringComparison.OrdinalIgnoreCase);
                    success = result.ExitCode == 0 && !hasError;
                }

                if (success)
                {
                    _logger.LogInformation("Dispositivo {BusId} conectado exitosamente.", busId);
                    return OperationResult.Ok();
                }
                else
                {
                    var details = !string.IsNullOrWhiteSpace(result.Error)
                        ? result.Error.Trim()
                        : result.Output.Trim();
                    if (details.Length > 512) details = details[..512];
                    var error = string.IsNullOrWhiteSpace(details)
                        ? $"usbip attach failed with exit code {result.ExitCode}."
                        : $"usbip attach failed with exit code {result.ExitCode}: {details}";
                    _logger.LogError("Fallo al conectar {BusId}. ExitCode: {ExitCode}, Output: {Output}, Error: {Error}",
                        busId, result.ExitCode, result.Output, result.Error);
                    return OperationResult.Fail(error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al conectar dispositivo {BusId}", busId);
                return OperationResult.Fail(ex.Message);
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
                (string ServerIP, string BusId)? targetBinding = null;
                if (int.TryParse(port.Trim(), out var portNum))
                {
                    if (!_portBindings.TryGetValue(portNum, out var cached))
                    {
                        var current = await GetConnectedDevicesAsync();
                        var matched = current.FirstOrDefault(d => d.Port == portNum);
                        if (matched != null && !string.IsNullOrWhiteSpace(matched.ServerIP) && !string.IsNullOrWhiteSpace(matched.BusId))
                            targetBinding = (matched.ServerIP, matched.BusId);
                    }
                    else
                    {
                        targetBinding = cached;
                    }
                }

                var result = await RunCommandAsync(new[] { "detach", "-p", port }, 5000, ct);

                // FIX: usbip detach produces empty output on success — use exit code
                bool hasError = result.Output.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                                result.Error.Contains("error", StringComparison.OrdinalIgnoreCase);

                bool success = result.ExitCode == 0 && !hasError;

                if (success)
                {
                    _logger.LogInformation("Puerto {Port} liberado.", port);
                    if (int.TryParse(port.Trim(), out var detachedPort))
                        _portBindings.TryRemove(detachedPort, out _);

                    if (targetBinding.HasValue)
                    {
                        await TryDynamicUnbindAsync(targetBinding.Value.ServerIP, targetBinding.Value.BusId, ct);
                    }
                }
                else
                {
                    _logger.LogError("Fallo al desconectar puerto {Port}. ExitCode: {ExitCode}, Error: {Error}",
                        port, result.ExitCode, result.Error);
                }

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

        internal void InvalidateIntegrityCache()
        {
            lock (_integrityCacheLock)
            {
                _cachedIntegritySignature = null;
                _cachedIntegrityResult = false;
            }
        }

        private static string BuildDirectoryIntegritySignature(string exePath)
        {
            var sb = new StringBuilder(256);
            var exeInfo = new FileInfo(exePath);
            sb.Append(exePath).Append('|')
              .Append(exeInfo.Exists ? exeInfo.Length : -1).Append('|')
              .Append(exeInfo.Exists ? exeInfo.LastWriteTimeUtc.Ticks : -1);

            var binDir = Path.GetDirectoryName(exePath);
            if (!string.IsNullOrEmpty(binDir) && Directory.Exists(binDir))
            {
                foreach (var dllName in RequiredNativeDlls)
                {
                    var fi = new FileInfo(Path.Combine(binDir, dllName));
                    sb.Append('|').Append(dllName).Append(':')
                      .Append(fi.Exists ? fi.Length : -1).Append(':')
                      .Append(fi.Exists ? fi.LastWriteTimeUtc.Ticks : -1);
                }

                foreach (var hijackName in HijackCandidateDlls)
                {
                    var fi = new FileInfo(Path.Combine(binDir, hijackName));
                    if (fi.Exists)
                    {
                        sb.Append("|H:").Append(hijackName).Append(':')
                          .Append(fi.Length).Append(':')
                          .Append(fi.LastWriteTimeUtc.Ticks);
                    }
                }
            }

            return sb.ToString();
        }

        private bool ValidateBinaryHash(string path)
        {
            try
            {
                string currentSignature = BuildDirectoryIntegritySignature(path);
                lock (_integrityCacheLock)
                {
                    if (_cachedIntegritySignature == currentSignature)
                    {
                        return _cachedIntegrityResult;
                    }
                }

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
                        lock (_integrityCacheLock)
                        {
                            _cachedIntegritySignature = currentSignature;
                            _cachedIntegrityResult = false;
                        }
                        return false;
                    }

                    // 2. Validate companion native DLLs and block known DLL hijacking vectors in the executable directory
                    var binDir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(binDir) && Directory.Exists(binDir))
                    {
                        foreach (var dllName in RequiredNativeDlls)
                        {
                            var dll = Path.Combine(binDir, dllName);
                            if (File.Exists(dll))
                            {
                                using var dllStream = File.OpenRead(dll);
                                var dllBytes = sha256.ComputeHash(dllStream);
                                var dllHash = BitConverter.ToString(dllBytes).Replace("-", "");

                                if (!ValidDllHashes.Contains(dllHash))
                                {
                                    _logger.LogCritical("SEGURIDAD: DLL nativa no autorizada o manipulada detectada: {Dll} (Hash: {Hash}). Operación abortada.", dll, dllHash);
                                    lock (_integrityCacheLock)
                                    {
                                        _cachedIntegritySignature = currentSignature;
                                        _cachedIntegrityResult = false;
                                    }
                                    return false;
                                }
                            }
                        }

                        foreach (var hijackName in HijackCandidateDlls)
                        {
                            var hijackPath = Path.Combine(binDir, hijackName);
                            if (File.Exists(hijackPath))
                            {
                                using var dllStream = File.OpenRead(hijackPath);
                                var dllBytes = sha256.ComputeHash(dllStream);
                                var dllHash = BitConverter.ToString(dllBytes).Replace("-", "");

                                if (!ValidDllHashes.Contains(dllHash))
                                {
                                    _logger.LogCritical("SEGURIDAD: DLL de secuestro detectada: {Dll} (Hash: {Hash}). Operación abortada.", hijackPath, dllHash);
                                    lock (_integrityCacheLock)
                                    {
                                        _cachedIntegritySignature = currentSignature;
                                        _cachedIntegrityResult = false;
                                    }
                                    return false;
                                }
                            }
                        }
                    }

                    lock (_integrityCacheLock)
                    {
                        _cachedIntegritySignature = currentSignature;
                        _cachedIntegrityResult = true;
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
            return await AttachDeviceWithResultAsync(serverIP, busId, receiveMode, CancellationToken.None);
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
                // Stop any active background kernel attach loops in usbip2_ude.sys first
                try { await RunCommandAsync(new[] { "attach", "-X" }, 3000, ct); } catch { }

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
                //        Server IP: 192.168.1.50
                //        busid: 1-1.4 (0bda:5100)
                //  or (usbip-win format):
                // Port 0: imported
                //     remote: 192.168.1.50
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

                    // Match modern usbip URI: "-> usbip://192.168.1.162:3240/5-1" or IPv6
                    var urlMatch = Regex.Match(line, @"usbip://\[?([a-zA-Z0-9\.\-:]+?)\]?(?::\d+)?/([\d\-\.]+)", RegexOptions.IgnoreCase);
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

                    // Match server IP (legacy / alt format, including IPv6)
                    var serverMatch = Regex.Match(line, @"(?:Server\s+IP|remote)\s*:\s*([a-zA-Z0-9\.\-:]+)", RegexOptions.IgnoreCase);
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

                foreach (var d in devices)
                {
                    if (d.Port >= 0 && !string.IsNullOrWhiteSpace(d.ServerIP) && !string.IsNullOrWhiteSpace(d.BusId))
                        _portBindings[d.Port] = (d.ServerIP, d.BusId);
                }

                _logger.LogDebug("GetConnectedDevicesAsync found {Count} connected device(s)", devices.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing connected devices from 'usbip port'");
            }

            return devices;
        }

        private static async Task<string> ReadBoundedUtf8Async(Stream stream, int maxBytes, bool stopAtNewLine, CancellationToken ct)
        {
            var buffer = new byte[Math.Min(4096, maxBytes)];
            using var ms = new MemoryStream();
            while (ms.Length < maxBytes)
            {
                int toRead = Math.Min(buffer.Length, maxBytes - (int)ms.Length);
                int read = await stream.ReadAsync(buffer.AsMemory(0, toRead), ct);
                if (read <= 0)
                    break;

                if (stopAtNewLine)
                {
                    int nlIdx = Array.IndexOf(buffer, (byte)'\n', 0, read);
                    if (nlIdx >= 0)
                    {
                        ms.Write(buffer, 0, nlIdx);
                        break;
                    }
                }

                ms.Write(buffer, 0, read);
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private async Task<(bool Allowed, bool BoundOnDemand, string? ErrorMessage)> TryDynamicBindAsync(string ip, string busId, CancellationToken ct)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(700);
                await tcp.ConnectAsync(ip, 3241, connectCts.Token);

                using var stream = tcp.GetStream();
                var msg = Encoding.UTF8.GetBytes($"BIND {busId}\n");
                await stream.WriteAsync(msg, 0, msg.Length, connectCts.Token);
                _logger.LogDebug("Comando dinámico BIND {BusId} enviado a {IP}:3241", busId, ip);

                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                readCts.CancelAfter(6000);
                var respLine = await ReadBoundedUtf8Async(stream, 4096, stopAtNewLine: true, readCts.Token);
                if (!string.IsNullOrWhiteSpace(respLine))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(respLine.Trim());
                    if (doc.RootElement.TryGetProperty("status", out var statusProp))
                    {
                        var status = statusProp.GetString() ?? "";
                        if (status.Equals("DENIED", StringComparison.OrdinalIgnoreCase) ||
                            status.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ||
                            status.Equals("UNSUPPORTED", StringComparison.OrdinalIgnoreCase))
                        {
                            var reason = doc.RootElement.TryGetProperty("message", out var msgProp)
                                ? msgProp.GetString()
                                : $"Server rejected the bind request for device {busId}";
                            return (false, false, reason);
                        }
                        if (status.Equals("OK", StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, true, null);
                        }
                    }
                }
                return (true, false, null);
            }
            catch
            {
                // Dynamic daemon not running or timeout; fallback to standard attach
                return (true, false, null);
            }
        }

        private async Task TryDynamicUnbindAsync(string ip, string busId, CancellationToken ct)
        {
            if (!InputValidator.IsValidHost(ip) || !InputValidator.IsValidBusId(busId))
                return;

            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(700);
                await tcp.ConnectAsync(ip, 3241, cts.Token);

                using var stream = tcp.GetStream();
                var msg = Encoding.UTF8.GetBytes($"UNBIND {busId}\n");
                await stream.WriteAsync(msg, 0, msg.Length, cts.Token);
                _logger.LogDebug("Comando dinámico UNBIND {BusId} enviado a {IP}:3241", busId, ip);

                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                readCts.CancelAfter(2500);
                await ReadBoundedUtf8Async(stream, 1024, stopAtNewLine: true, readCts.Token);
            }
            catch
            {
                // Best-effort instant server release; auto-unbind daemon handles fallback
            }
        }

        private async Task TryQueryDynamicInventoryAsync(string ip, List<UsbDevice> devices, CancellationToken ct)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(1200);
                await tcp.ConnectAsync(ip, 3241, cts.Token);
                using var stream = tcp.GetStream();
                var msg = Encoding.UTF8.GetBytes("LIST\n");
                await stream.WriteAsync(msg, 0, msg.Length, cts.Token);

                string json = (await ReadBoundedUtf8Async(stream, 65536, stopAtNewLine: false, cts.Token)).Trim();
                if (!string.IsNullOrEmpty(json))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("devices", out var devsProp) && devsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var existing = new HashSet<string>(devices.Select(d => d.BusId), StringComparer.OrdinalIgnoreCase);
                        foreach (var item in devsProp.EnumerateArray())
                        {
                            if (TryParseDynamicInventoryItem(item, ip, out var device) &&
                                device is not null &&
                                !existing.Contains(device.BusId))
                            {
                                devices.Add(device);
                                existing.Add(device.BusId);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Dynamic inventory not available
            }
        }

        internal static bool TryParseDynamicInventoryItem(JsonElement item, string ip, out UsbDevice? device)
        {
            device = null;
            string busId;
            string description;
            string hardwareId;

            if (item.ValueKind == JsonValueKind.String)
            {
                var line = item.GetString();
                if (string.IsNullOrWhiteSpace(line)) return false;

                var match = Regex.Match(
                    line,
                    @"^\s*([0-9a-zA-Z\-\.]+)\s*:\s*(.+?)\s*(?:\(([0-9a-fA-F]{4}:[0-9a-fA-F]{4})\))?$");
                if (!match.Success) return false;

                busId = match.Groups[1].Value.Trim();
                description = match.Groups[2].Value.Trim();
                hardwareId = match.Groups[3].Success ? match.Groups[3].Value.Trim() : string.Empty;
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                busId = ReadJsonString(item, "busid");
                description = ReadJsonString(item, "description");
                var vidPid = ReadJsonString(item, "vid_pid");
                if (!Regex.IsMatch(vidPid, @"^[0-9a-fA-F]{4}:[0-9a-fA-F]{4}$"))
                {
                    var vendorId = ReadJsonString(item, "vid");
                    var productId = ReadJsonString(item, "pid");
                    vidPid = Regex.IsMatch(vendorId, @"^[0-9a-fA-F]{4}$") &&
                        Regex.IsMatch(productId, @"^[0-9a-fA-F]{4}$")
                        ? $"{vendorId}:{productId}"
                        : string.Empty;
                }

                hardwareId = vidPid;
            }
            else
            {
                return false;
            }

            if (!InputValidator.IsValidBusId(busId)) return false;

            device = new UsbDevice
            {
                BusId = busId,
                HardwareId = hardwareId,
                Description = string.IsNullOrWhiteSpace(description) ? "USB Device" : description,
                RemoteIp = ip,
                ServerIP = ip,
                IsConnected = false
            };
            return true;
        }

        private static string ReadJsonString(JsonElement item, string propertyName)
        {
            if (!item.TryGetProperty(propertyName, out var property)) return string.Empty;
            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString() ?? string.Empty,
                JsonValueKind.Number => property.GetRawText(),
                _ => string.Empty
            };
        }
    }
}
