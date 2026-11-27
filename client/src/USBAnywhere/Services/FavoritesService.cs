using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using USBAnywhere.Models;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// Favorites service - Manage favorite devices with auto-connect and smart hostname/VID:PID resolution
/// </summary>
public class FavoritesService : IFavoritesService
{
    private readonly IConfigService _config;
    private readonly IUsbipService _usbip;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _hostResolver;

    public FavoritesService(
        IConfigService config,
        IUsbipService usbip,
        Func<string, CancellationToken, Task<IPAddress[]>>? hostResolver = null)
    {
        _config = config;
        _usbip = usbip;
        _hostResolver = hostResolver ?? Dns.GetHostAddressesAsync;
    }

    public List<FavoriteDevice> GetFavorites()
    {
        var config = _config.Load();
        return config.Favorites ?? new List<FavoriteDevice>();
    }

    public OperationResult AddFavorite(string serverIP, string busId, string description, bool autoConnect = true, string receiveMode = "zero-copy")
        => AddFavorite(serverIP, busId, description, autoConnect, receiveMode, hardwareId: null, serverHostname: null, deviceSerial: null, revision: null);

    public OperationResult AddFavorite(
        string serverIP,
        string busId,
        string description,
        bool autoConnect,
        string receiveMode,
        string? hardwareId,
        string? serverHostname = null,
        string? deviceSerial = null,
        string? revision = null)
    {
        var config = _config.Load();
        config.Favorites ??= new List<FavoriteDevice>();

        // Check if already exists
        if (config.Favorites.Any(f => f.ServerIP == serverIP && f.BusId == busId))
            return new OperationResult { Success = false, Error = "Already in favorites" };

        var effectiveMode = UsbipService.ResolveEffectiveReceiveMode(
            string.IsNullOrWhiteSpace(receiveMode) ? "zero-copy" : receiveMode,
            description,
            null);

        // Add favorite
        config.Favorites.Add(new FavoriteDevice
        {
            ServerIP = serverIP,
            BusId = busId,
            Description = description,
            AutoConnect = autoConnect,
            AddedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ReceiveMode = effectiveMode,
            HardwareId = string.IsNullOrWhiteSpace(hardwareId) ? null : hardwareId.Trim().ToLowerInvariant(),
            ServerHostname = string.IsNullOrWhiteSpace(serverHostname) ? null : serverHostname.Trim(),
            DeviceSerial = string.IsNullOrWhiteSpace(deviceSerial) ? null : deviceSerial.Trim(),
            Revision = string.IsNullOrWhiteSpace(revision) ? null : revision.Trim()
        });

        _config.Save(config);
        return new OperationResult { Success = true };
    }

    public async Task<(string ServerIP, string BusId, bool ResolvedChanged)> ResolveFavoriteTargetAsync(
        FavoriteDevice favorite,
        CancellationToken ct = default)
    {
        string targetIp = favorite.ServerIP;
        string targetBusId = favorite.BusId;
        bool changed = false;

        List<UsbDevice> remoteDevices = new();
        try
        {
            remoteDevices = await _usbip.ListRemoteDevicesAsync(targetIp, ct);
        }
        catch
        {
            remoteDevices = new();
        }

        if (remoteDevices.Count == 0 && !string.IsNullOrWhiteSpace(favorite.ServerHostname))
        {
            try
            {
                var addrs = await _hostResolver(favorite.ServerHostname.Trim(), ct);
                var candidate = addrs
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    ?? addrs.FirstOrDefault(a => !IPAddress.IsLoopback(a))
                    ?? addrs.FirstOrDefault();
                if (candidate != null)
                {
                    var resolvedIp = candidate.ToString();
                    if (!string.Equals(resolvedIp, targetIp, StringComparison.OrdinalIgnoreCase))
                    {
                        var resolvedDevs = await _usbip.ListRemoteDevicesAsync(resolvedIp, ct);
                        if (resolvedDevs.Count > 0)
                        {
                            targetIp = resolvedIp;
                            remoteDevices = resolvedDevs;
                            changed = true;
                        }
                    }
                }
            }
            catch
            {
                // Keep original IP when DNS/mDNS lookup is unavailable
            }
        }

        if (remoteDevices.Count > 0)
        {
            var exactBus = remoteDevices.FirstOrDefault(d =>
                string.Equals(d.BusId, targetBusId, StringComparison.OrdinalIgnoreCase));

            bool busStillMatchesHardware = exactBus != null &&
                (string.IsNullOrWhiteSpace(favorite.HardwareId) ||
                 string.Equals(exactBus.HardwareId, favorite.HardwareId, StringComparison.OrdinalIgnoreCase));

            if (!busStillMatchesHardware && !string.IsNullOrWhiteSpace(favorite.HardwareId))
            {
                var matches = remoteDevices
                    .Where(d => string.Equals(d.HardwareId, favorite.HardwareId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                UsbDevice? remapped = null;
                if (matches.Count == 1)
                {
                    remapped = matches[0];
                }
                else if (matches.Count > 1 && !string.IsNullOrWhiteSpace(favorite.Revision))
                {
                    var revMatches = matches
                        .Where(d => string.Equals(d.Revision, favorite.Revision, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (revMatches.Count == 1)
                    {
                        remapped = revMatches[0];
                    }
                }
                if (remapped == null && matches.Count > 1 && !string.IsNullOrWhiteSpace(favorite.Description))
                {
                    var descMatches = matches
                        .Where(d => string.Equals(d.Description, favorite.Description, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (descMatches.Count == 1)
                    {
                        remapped = descMatches[0];
                    }
                }

                if (remapped != null && !string.Equals(remapped.BusId, targetBusId, StringComparison.OrdinalIgnoreCase))
                {
                    targetBusId = remapped.BusId;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            try
            {
                var config = _config.Load();
                config.Favorites ??= new List<FavoriteDevice>();
                var stored = config.Favorites.FirstOrDefault(f =>
                    string.Equals(f.ServerIP, favorite.ServerIP, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.BusId, favorite.BusId, StringComparison.OrdinalIgnoreCase));
                if (stored != null)
                {
                    stored.ServerIP = targetIp;
                    stored.BusId = targetBusId;
                    _config.Save(config);
                }
                favorite.ServerIP = targetIp;
                favorite.BusId = targetBusId;
            }
            catch
            {
                // Best-effort persistence
            }
        }

        return (targetIp, targetBusId, changed);
    }

    public OperationResult RemoveFavorite(string serverIP, string busId)
    {
        var config = _config.Load();
        config.Favorites ??= new List<FavoriteDevice>();

        var toRemove = config.Favorites.FirstOrDefault(f => f.ServerIP == serverIP && f.BusId == busId);
        if (toRemove != null)
        {
            config.Favorites.Remove(toRemove);
            _config.Save(config);
            return new OperationResult { Success = true };
        }

        return new OperationResult { Success = false, Error = "Not found in favorites" };
    }

    public bool IsFavorite(string serverIP, string busId)
    {
        var favorites = GetFavorites();
        return favorites.Any(f => f.ServerIP == serverIP && f.BusId == busId);
    }

    public async Task<List<FavoriteConnectResult>> ConnectFavoritesAsync()
    {
        var results = new List<FavoriteConnectResult>();
        var favorites = GetFavorites().Where(f => f.AutoConnect).ToList();

        foreach (var fav in favorites)
        {
            var requested = string.IsNullOrWhiteSpace(fav.ReceiveMode) ? "zero-copy" : fav.ReceiveMode;
            var mode = UsbipService.ResolveEffectiveReceiveMode(requested, fav.Description, null);
            var (resolvedIp, resolvedBusId, _) = await ResolveFavoriteTargetAsync(fav);
            var result = await _usbip.ConnectAsync(resolvedIp, resolvedBusId, mode, serial: fav.CustomSerial);
            results.Add(new FavoriteConnectResult
            {
                Device = $"{resolvedIp}/{resolvedBusId}",
                Success = result.Success,
                Error = result.Error
            });
        }

        return results;
    }
}
