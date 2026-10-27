// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu - GPL v3

using System;
using System.IO;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// Resolves all application paths.
/// Portable mode: everything lives next to the .exe
/// Installed mode: data goes to %AppData%\USBAnywhere
/// </summary>
public class EnvironmentService : IEnvironmentService
{
    private string _appDataPath = string.Empty;

    public string AppDataPath => _appDataPath;
    public string LogsPath    => Path.Combine(_appDataPath, "logs");
    public string TempPath    => Path.Combine(_appDataPath, "temp");
    public bool   IsPortableMode { get; private set; }
    public bool   IsPortable     => IsPortableMode;

    /// <summary>
    /// BinPath: where usbipw.exe lives.
    /// Portable → next to exe (BaseDirectory). Installed → BaseDirectory/bin.
    /// </summary>
    public string BinPath
    {
        get
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var subDir = Path.Combine(baseDir, "bin");
            if (Directory.Exists(subDir) && 
                (File.Exists(Path.Combine(subDir, "usbipw.exe")) || 
                 File.Exists(Path.Combine(subDir, "usbip.exe"))))
                return subDir;
            return baseDir;
        }
    }

    /// <summary>
    /// DriversPath: where .inf/.sys driver files live.
    /// </summary>
    public string DriversPath
    {
        get
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var subDir = Path.Combine(baseDir, "drivers");
            if (Directory.Exists(subDir)) return subDir;
            return baseDir;
        }
    }

    public void Initialize()
    {
        var baseDir   = AppDomain.CurrentDomain.BaseDirectory;
        var portFlag  = Path.Combine(baseDir, "portable.flag");
        var dataLocal = Path.Combine(baseDir, "data");

        IsPortableMode = File.Exists(portFlag) || File.Exists(Path.Combine(baseDir, ".portable"));

        _appDataPath = IsPortableMode
            ? dataLocal
            : Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "USBAnywhere");

        EnsureDirectories();
        MigrateLegacyConfigIfNeeded();
        CleanTempDirectory();
    }

    public string GetConfigFilePath() => Path.Combine(_appDataPath, "config.json");

    private void EnsureDirectories()
    {
        foreach (var dir in new[] { _appDataPath, LogsPath, TempPath })
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }

    private void MigrateLegacyConfigIfNeeded()
    {
        try
        {
            var currentConfig = GetConfigFilePath();
            if (!IsPortableMode && !File.Exists(currentConfig))
            {
                var legacyConfig = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                    "SnakeUSBIP",
                    "config.json");
                if (File.Exists(legacyConfig))
                {
                    File.Copy(legacyConfig, currentConfig, overwrite: false);
                }
            }
        }
        catch { }
    }

    private void CleanTempDirectory()
    {
        try
        {
            if (Directory.Exists(TempPath))
            {
                var files = Directory.GetFiles(TempPath);
                foreach (var file in files)
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        catch { }
    }
}
