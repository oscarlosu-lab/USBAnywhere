// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

using System;
using System.IO;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// File-based logger service. Writes daily log files to the configured logs directory.
/// </summary>
public class LoggerService : ILoggerService
{
    private readonly IEnvironmentService _env;
    private readonly object _logLock = new();
    private string _logFile = string.Empty;

    public LoggerService(IEnvironmentService env)
    {
        _env = env;
    }

    public void Initialize()
    {
        var logsDir = _env.LogsPath;
        if (!Directory.Exists(logsDir))
            Directory.CreateDirectory(logsDir);

        _logFile = Path.Combine(logsDir, $"usbanywhere_{DateTime.Now:yyyyMMdd}.log");

        CleanupOldLogs(logsDir, keepDays: 7);

        Info("=== USBAnywhere v3.0.0 Logger Initialized ===");
    }

    public void Info(string message)    => Write("INFO",    message);
    public void Warning(string message) => Write("WARN",    message);
    public void Debug(string message)   => Write("DEBUG",   message);

    public void Error(string message, Exception? ex = null)
    {
        Write("ERROR", ex != null ? $"{message} | {ex.GetType().Name}: {ex.Message}" : message);
        if (ex?.StackTrace != null)
            Write("TRACE", ex.StackTrace);
    }

    // ── private ──────────────────────────────────────────────────────────────

    private void Write(string level, string message)
    {
        try
        {
            if (string.IsNullOrEmpty(_logFile)) return;
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level,-5}] {message}";
            lock (_logLock)
            {
                File.AppendAllText(_logFile, line + Environment.NewLine);
            }
        }
        catch { /* never crash on logging */ }
    }

    private static void CleanupOldLogs(string dir, int keepDays)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-keepDays);
            foreach (var f in Directory.GetFiles(dir, "usbanywhere_*.log"))
                if (File.GetCreationTime(f) < cutoff)
                    File.Delete(f);
        }
        catch (Exception dbgEx) { System.Diagnostics.Debug.WriteLine($"[LoggerService] {dbgEx.Message}"); }
    }
}
