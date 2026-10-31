// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

namespace USBAnywhere.Services.Interfaces;

/// <summary>
/// Simple app-level logger (wraps file logging).
/// Uses custom methods (Info/Warning/Error/Debug) to avoid coupling to ILogger<T>.
/// </summary>
public interface ILoggerService
{
    void Initialize();
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? ex = null);
    void Debug(string message);
}
