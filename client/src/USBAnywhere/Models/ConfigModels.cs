// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025 SnakeFoxu - GPL v3

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
    public string DefaultReceiveMode { get; set; } = "zero-copy";
    public bool AutoDetachOnExit { get; set; } = true;
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
    public string ReceiveMode { get; set; } = "zero-copy";
}
