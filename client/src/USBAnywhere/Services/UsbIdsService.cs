/*
 * USBAnywhere - USB ID Database Service
 * (c) 2025-2026 SnakeFoxu - https://github.com/oscarlosu-lab/USBAnywhere
 * GPL v3 License
 */

using System.IO;
using System.Text.RegularExpressions;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// USB ID Database service - Maps VID:PID to vendor/product names
/// Created by SnakeFoxu (OscarLosu)
/// </summary>
public class UsbIdsService : IUsbIdsService
{
    private static readonly Regex VendorRegex = new(@"^([0-9a-fA-F]{4})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex ProductRegex = new(@"^\t([0-9a-fA-F]{4})\s+(.+)$", RegexOptions.Compiled);

    private readonly Dictionary<string, string> _vendors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _products = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _customNames = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded = false;

    /// <summary>
    /// Built-in common USB vendor/product IDs (embedded database)
    /// </summary>
    private static readonly Dictionary<string, string> CommonVendors = new()
    {
        { "0bda", "Realtek Semiconductor Corp." },
        { "046d", "Logitech, Inc." },
        { "8087", "Intel Corp." },
        { "1d6b", "Linux Foundation" },
        { "0951", "Kingston Technology" },
        { "045e", "Microsoft Corp." },
        { "0781", "SanDisk Corp." },
        { "05ac", "Apple, Inc." },
        { "2109", "VIA Labs, Inc." },
        { "174c", "ASMedia Technology Inc." },
        { "04f2", "Chicony Electronics Co., Ltd" },
        { "0489", "Foxconn / Hon Hai" },
        { "148f", "Ralink Technology, Corp." },
        { "0cf3", "Qualcomm Atheros Communications" },
        { "413c", "Dell Computer Corp." },
        { "1bcf", "Sunplus Innovation Technology Inc." },
        { "0930", "Toshiba Corp." },
        { "8086", "Intel Corp." },
        { "058f", "Alcor Micro Corp." },
        { "0b95", "ASIX Electronics Corp." },
        { "0e8d", "MediaTek Inc." },
        { "03f0", "HP, Inc" },
        { "04e8", "Samsung Electronics Co., Ltd" },
        { "1058", "Western Digital Technologies, Inc." },
        { "0bc2", "Seagate RSS LLC" },
        { "18d1", "Google Inc." },
        { "2357", "TP-Link" },
        { "0458", "KYE Systems Corp." },
        { "1a86", "QinHeng Electronics" },
        { "10c4", "Silicon Labs" },
        { "067b", "Prolific Technology, Inc." },
        { "0403", "Future Technology Devices International, Ltd" }
    };

    private static readonly Dictionary<string, string> CommonProducts = new()
    {
        { "0bda:5100", "Acmer Laser Camera" },
        { "0bda:8153", "RTL8153 Gigabit Ethernet Adapter" },
        { "0bda:8152", "RTL8152 Fast Ethernet Adapter" },
        { "046d:c077", "M105 Optical Mouse" },
        { "046d:c52b", "Unifying Receiver" },
        { "0951:16a4", "DataTraveler 3.0" },
        { "0781:5581", "Ultra" },
        { "1a86:7523", "CH340 Serial" },
        { "10c4:ea60", "CP210x UART Bridge" },
        { "067b:2303", "PL2303 Serial Port" },
        { "0403:6001", "FT232 Serial" }
    };

    public UsbIdsService()
    {
        LoadBuiltInDatabase();
        var defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "usb.ids");
        if (File.Exists(defaultPath))
        {
            ParseLines(File.ReadLines(defaultPath));
        }
    }

    /// <summary>
    /// Normalize a 4-digit hex VID/PID (strips optional "0x" prefix without stripping leading zeros).
    /// </summary>
    private static string NormalizeHexId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        id = id.Trim().ToLowerInvariant();
        if (id.StartsWith("0x", StringComparison.Ordinal))
            id = id[2..];
        return id.PadLeft(4, '0');
    }

    private void LoadBuiltInDatabase()
    {
        foreach (var kv in CommonVendors)
        {
            _vendors[kv.Key.ToLowerInvariant()] = kv.Value;
        }

        foreach (var kv in CommonProducts)
        {
            _products[kv.Key.ToLowerInvariant()] = kv.Value;
        }

        _loaded = true;
    }

    private void ParseLines(IEnumerable<string> lines)
    {
        try
        {
            string currentVendor = "";

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line) || line[0] == '#')
                    continue;

                // Vendor line: starts at column 0, format: "XXXX  Name"
                if (line[0] != '\t' && line[0] != ' ')
                {
                    var match = VendorRegex.Match(line);
                    if (match.Success)
                    {
                        currentVendor = match.Groups[1].Value.ToLowerInvariant();
                        _vendors[currentVendor] = match.Groups[2].Value.Trim();
                    }
                }
                // Product line: starts with single tab, format: "\tXXXX  Name"
                else if (line[0] == '\t' && (line.Length < 2 || line[1] != '\t'))
                {
                    var match = ProductRegex.Match(line);
                    if (match.Success && !string.IsNullOrEmpty(currentVendor))
                    {
                        var productId = match.Groups[1].Value.ToLowerInvariant();
                        var key = $"{currentVendor}:{productId}";
                        _products[key] = match.Groups[2].Value.Trim();
                    }
                }
            }

            _loaded = true;
        }
        catch (Exception dbgEx)
        {
            System.Diagnostics.Debug.WriteLine($"[UsbIdsService] {dbgEx.Message}");
        }
    }

    /// <summary>
    /// Load USB IDs from usb.ids file if available
    /// </summary>
    public async Task LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            ParseLines(lines);
        }
        catch (Exception dbgEx)
        {
            System.Diagnostics.Debug.WriteLine($"[UsbIdsService] {dbgEx.Message}");
        }
    }

    /// <summary>
    /// Get vendor name from VID
    /// </summary>
    public string GetVendor(string vendorId)
    {
        var vid = NormalizeHexId(vendorId);
        return _vendors.TryGetValue(vid, out var name) ? name : $"Unknown ({vid})";
    }

    /// <summary>
    /// Get product name from VID:PID
    /// </summary>
    public string GetProduct(string vendorId, string productId)
    {
        var vid = NormalizeHexId(vendorId);
        var pid = NormalizeHexId(productId);
        var key = $"{vid}:{pid}";

        return _products.TryGetValue(key, out var name) ? name : $"Unknown ({key})";
    }

    /// <summary>
    /// Get full device description - prioritizes custom names
    /// </summary>
    public string GetDeviceDescription(string vendorId, string productId)
    {
        var customName = GetCustomName(vendorId, productId);
        if (customName != null) return customName;

        var vid = NormalizeHexId(vendorId);
        var pid = NormalizeHexId(productId);
        var vendor = GetVendor(vid);
        var product = GetProduct(vid, pid);

        if (vendor.StartsWith("Unknown") && product.StartsWith("Unknown"))
            return $"USB Device ({vid}:{pid})";

        if (product.StartsWith("Unknown"))
            return $"{vendor} ({vid}:{pid})";

        return $"{vendor} : {product}";
    }

    /// <summary>
    /// Set custom name for a device (by VID:PID)
    /// </summary>
    public void SetCustomName(string vendorId, string productId, string name)
    {
        var key = $"{NormalizeHexId(vendorId)}:{NormalizeHexId(productId)}";
        _customNames[key] = name;
    }

    /// <summary>
    /// Get custom name for a device (returns null if not set)
    /// </summary>
    public string? GetCustomName(string vendorId, string productId)
    {
        var key = $"{NormalizeHexId(vendorId)}:{NormalizeHexId(productId)}";
        return _customNames.TryGetValue(key, out var name) ? name : null;
    }

    /// <summary>
    /// Load custom names from config
    /// </summary>
    public void LoadCustomNames(Dictionary<string, string>? names)
    {
        if (names == null) return;
        foreach (var kv in names)
            _customNames[kv.Key.ToLowerInvariant()] = kv.Value;
    }

    /// <summary>
    /// Get all custom names for saving to config
    /// </summary>
    public Dictionary<string, string> GetCustomNames() => new(_customNames);

    public bool IsLoaded    => _loaded;
    public int VendorCount  => _vendors.Count;
    public int ProductCount => _products.Count;

    // IUsbIdsService alias methods
    public string GetDeviceName(string vendorId, string productId)   => GetDeviceDescription(vendorId, productId);
    public string GetVendorName(string vendorId)                      => GetVendor(vendorId);
}
