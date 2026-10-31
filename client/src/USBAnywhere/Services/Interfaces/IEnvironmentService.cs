// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

namespace USBAnywhere.Services.Interfaces;

/// <summary>
/// Provides resolved paths for all app directories (Data, Logs, Bin, Drivers, Temp, Config)
/// </summary>
public interface IEnvironmentService
{
    /// <summary>App data root (portable: next to exe | installed: %AppData%\USBAnywhere)</summary>
    string AppDataPath { get; }
    string LogsPath { get; }
    string BinPath { get; }
    string DriversPath { get; }
    string TempPath { get; }

    string GetConfigFilePath();
    void Initialize();
    bool IsPortableMode { get; }
    bool IsPortable { get; }
}
