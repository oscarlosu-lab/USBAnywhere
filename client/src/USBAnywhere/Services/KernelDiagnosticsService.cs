// USBAnywhere - Kernel WPP/ETW Tracing & Diagnostic Bundle Service
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

using USBAnywhere.Models;

namespace USBAnywhere.Services;

public static class KernelDiagnosticsService
{
    public const string UdeTraceProviderGuid = "{ed18c9c5-8322-48ae-bf78-d01d898a1562}";
    public const string FilterTraceProviderGuid = "{90c336ed-69fb-43d6-b800-1552d72d200b}";

    public static string ExportDiagnosticBundle(
        string? outputZipPath = null,
        IEnumerable<string>? recentUiLogs = null,
        string? driverStatusText = null,
        IReadOnlyList<ConnectedDevice>? connectedDevices = null,
        IReadOnlyList<VhciPersistentDevice>? persistentDevices = null)
    {
        string targetZip = string.IsNullOrWhiteSpace(outputZipPath)
            ? Path.Combine(Path.GetTempPath(), $"USBAnywhere_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip")
            : outputZipPath;

        string? parentDir = Path.GetDirectoryName(targetZip);
        if (!string.IsNullOrEmpty(parentDir))
            Directory.CreateDirectory(parentDir);

        string staging = Path.Combine(Path.GetTempPath(), $"USBAnywhere_diag_{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== USBAnywhere Diagnostic Report ({DateTimeOffset.Now:O}) ===");
            sb.AppendLine($"OS: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
            sb.AppendLine($"Driver Status: {driverStatusText ?? "unknown"}");

            var hub = KernelConfigService.GetVirtualHubPortSettings();
            sb.AppendLine($"Virtual Hub Ports: USB 2.0={hub.Usb20Ports}, USB 3.0={hub.Usb30Ports}");

            sb.AppendLine($"Connected Devices: {connectedDevices?.Count ?? 0}");
            if (connectedDevices != null)
            {
                foreach (var d in connectedDevices)
                    sb.AppendLine($"  Port {d.Port}: {d.ServerIP} / {d.BusId} ({d.VendorId}:{d.ProductId}) Serial='{d.Serial}' {d.Description}");
            }

            sb.AppendLine($"Kernel Persistent Devices: {persistentDevices?.Count ?? 0}");
            if (persistentDevices != null)
            {
                foreach (var p in persistentDevices)
                    sb.AppendLine($"  {p.Host}:{p.Service} {p.BusId} Serial='{p.Serial}' Mode={p.ReceiveMode} Once={p.AttachOnce}");
            }

            File.WriteAllText(Path.Combine(staging, "summary.txt"), sb.ToString(), new UTF8Encoding(false));

            if (recentUiLogs != null)
                File.WriteAllText(Path.Combine(staging, "ui_activity.log"), string.Join(Environment.NewLine, recentUiLogs), new UTF8Encoding(false));

            string appDataLogs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "USBAnywhere", "logs");
            if (Directory.Exists(appDataLogs))
            {
                foreach (string logFile in Directory.GetFiles(appDataLogs, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(3))
                    File.Copy(logFile, Path.Combine(staging, Path.GetFileName(logFile)), overwrite: true);
            }

            if (File.Exists(targetZip))
                File.Delete(targetZip);
            ZipFile.CreateFromDirectory(staging, targetZip);
            return targetZip;
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch { }
        }
    }

    public static async Task<OperationResult> CaptureKernelEtwSnapshotAsync(string outputEtlBasePath, int durationSeconds = 3, CancellationToken ct = default)
    {
        try
        {
            string logman = Path.Combine(Environment.SystemDirectory, "logman.exe");
            if (!File.Exists(logman))
                return OperationResult.Fail("logman.exe not found on system.");

            string sessionName = $"USBAnywhere_KernelTrace";
            string etlPath = outputEtlBasePath.EndsWith(".etl", StringComparison.OrdinalIgnoreCase)
                ? outputEtlBasePath
                : outputEtlBasePath + ".etl";

            await RunProcessAsync(logman, new[] { "stop", sessionName, "-ets" }, 3000, ct);
            var start = await RunProcessAsync(logman, new[] { "create", "trace", sessionName, "-p", UdeTraceProviderGuid, "0xF", "5", "-o", etlPath, "-ets" }, 5000, ct);
            if (start.ExitCode != 0)
                return OperationResult.Fail($"Unable to start ETW session (Administrator privileges may be required): {start.Error}");

            await RunProcessAsync(logman, new[] { "update", sessionName, "-p", FilterTraceProviderGuid, "0x3", "5", "-ets" }, 3000, ct);
            await Task.Delay(Math.Clamp(durationSeconds, 1, 15) * 1000, ct);
            await RunProcessAsync(logman, new[] { "stop", sessionName, "-ets" }, 5000, ct);

            return File.Exists(etlPath)
                ? OperationResult.Ok(etlPath)
                : OperationResult.Fail("ETW trace file was not created.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    private static async Task<(int ExitCode, string Error)> RunProcessAsync(string exe, string[] args, int timeoutMs, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string a in args)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi);
        if (proc == null)
            return (-1, "Failed to start process.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);
        string err = await proc.StandardError.ReadToEndAsync(timeoutCts.Token);
        await proc.WaitForExitAsync(timeoutCts.Token);
        return (proc.ExitCode, err.Trim());
    }
}
