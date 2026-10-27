// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025 SnakeFoxu - GPL v3

using System;
using System.Collections.Generic;

namespace USBAnywhere.Models;

/// <summary>
/// A remote USB device available on a USBIP server
/// </summary>
public class UsbDevice
{
    public string BusId { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RemoteIp { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    
    /// <summary>
    /// Extract VendorId from HardwareId (format: "XXXX:YYYY")
    /// Safe: returns empty string if format is invalid
    /// </summary>
    public string VendorId
    {
        get
        {
            if (string.IsNullOrEmpty(HardwareId)) return string.Empty;
            var colonIdx = HardwareId.IndexOf(':');
            return colonIdx >= 4 ? HardwareId[..colonIdx] : 
                   HardwareId.Length >= 4 ? HardwareId[..4] : string.Empty;
        }
    }
    
    /// <summary>
    /// Extract ProductId from HardwareId (format: "XXXX:YYYY")
    /// Safe: returns empty string if format is invalid
    /// </summary>
    public string ProductId
    {
        get
        {
            if (string.IsNullOrEmpty(HardwareId)) return string.Empty;
            var colonIdx = HardwareId.IndexOf(':');
            if (colonIdx >= 0 && colonIdx + 1 < HardwareId.Length)
                return HardwareId[(colonIdx + 1)..];
            return string.Empty;
        }
    }
    
    public string ServerIP { get; set; } = string.Empty;
}

/// <summary>
/// A locally attached/connected device (result of usbip port)
/// </summary>
public class ConnectedDevice
{
    public int Port { get; set; }
    public string BusId { get; set; } = string.Empty;
    public string ServerIP { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VendorId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
}

/// <summary>
/// Driver installation status
/// </summary>
public class DriverStatus
{
    public bool Installed { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Generic operation result
/// </summary>
public class OperationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }

    public static OperationResult Ok(string? message = null)
        => new() { Success = true, Message = message };

    public static OperationResult Fail(string error)
        => new() { Success = false, Error = error };
}

/// <summary>
/// Update check result
/// </summary>
public class UpdateInfo
{
    public bool UpdateAvailable { get; set; }
    public string? LatestVersion { get; set; }
    public string? CurrentVersion { get; set; }
    public string? DownloadUrl { get; set; }
    public string? InstallerName { get; set; }
    public string? HashUrl { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// VPN peer (Tailscale / ZeroTier / WireGuard)
/// </summary>
public class VpnPeer
{
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; }
    public string Source { get; set; } = string.Empty; // "Tailscale", "ZeroTier", "WireGuard"
    public bool HasUsbip { get; set; }
}

/// <summary>
/// Result of a favorite auto-connect attempt
/// </summary>
public class FavoriteConnectResult
{
    public string Device { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
