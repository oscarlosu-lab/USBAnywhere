// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

using System.Collections.Generic;

namespace USBAnywhere.Models;

/// <summary>
/// Application configuration — persisted to JSON
/// </summary>
public class AppConfig
{
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "dark";
    public List<FavoriteDevice> Favorites { get; set; } = new();
    public Dictionary<string, string>? CustomDeviceNames { get; set; }
    public string? LastSubnet { get; set; }
    public bool AutoScanOnStart { get; set; } = true;
    public bool AutoConnectFavorites { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public string? LastServerIP { get; set; }
    public string DefaultReceiveMode { get; set; } = "low-latency";
    public bool AutoDetachOnExit { get; set; } = true;
    public bool AutoDetachOnSleep { get; set; } = true;
    public string DisconnectAllHotkey { get; set; } = "Ctrl+Alt+D";
    public Dictionary<string, string>? CustomDeviceSerials { get; set; }
    public List<PairedServerEntry> PairedServers { get; set; } = new();
    public bool LocalApiEnabled { get; set; } = false;
    public int LocalApiPort { get; set; } = 13241;
    public string LocalApiToken { get; set; } = string.Empty;
    public bool EnforceSessionIsolation { get; set; } = true;
}

public class PairedServerEntry
{
    public string Host { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string ControlToken { get; set; } = string.Empty;
    public int TlsPort { get; set; } = 3243;
    public string TlsFingerprint { get; set; } = string.Empty;
    public bool UseTls { get; set; }
    public string PairedAtUtc { get; set; } = string.Empty;
}

/// <summary>
/// A saved favorite USB/IP device
/// </summary>
public class FavoriteDevice
{
    public string ServerIP { get; set; } = string.Empty;
    public string BusId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool AutoConnect { get; set; } = true;
    public string AddedDate { get; set; } = string.Empty;
    public string ReceiveMode { get; set; } = "low-latency";
    public string? CustomSerial { get; set; }
    public bool KernelPersistent { get; set; }
    public string? ServerHostname { get; set; }
    public string? HardwareId { get; set; }
    public string? Revision { get; set; }
    public string? DeviceSerial { get; set; }
    public string AttachMode { get; set; } = "always";
}
