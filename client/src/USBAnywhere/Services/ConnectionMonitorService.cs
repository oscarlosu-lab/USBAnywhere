using System.Timers;
using Timer = System.Timers.Timer;

using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// Connection monitor for auto-reconnection
/// Port of PowerShell connection monitoring logic
/// </summary>
public class ConnectionMonitorService : IConnectionMonitorService, IDisposable
{
    private readonly IUsbipService _usbip;
    private readonly IFavoritesService _favorites;
    private readonly Timer _monitorTimer;
    private readonly List<MonitoredDevice> _monitoredDevices = new();
    private readonly object _lock = new();
    private bool _isRunning = false;
    private bool _disposed = false;

    public event EventHandler<ConnectionLostEventArgs>? ConnectionLost;
    public event EventHandler<ConnectionRestoredEventArgs>? ConnectionRestored;

    public ConnectionMonitorService(IUsbipService usbip, IFavoritesService favorites)
    {
        _usbip = usbip;
        _favorites = favorites;

        // Check every 2 seconds for faster detection, sequential ticks (no re-entrancy)
        _monitorTimer = new Timer(2000);
        _monitorTimer.Elapsed += OnMonitorTick;
        _monitorTimer.AutoReset = false;
    }

    /// <summary>
    /// Start monitoring connections
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning || _disposed) return;
            _isRunning = true;
        }
        try { _monitorTimer.Start(); } catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// Stop monitoring
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _isRunning = false;
        }
        try { _monitorTimer.Stop(); } catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// Add device to monitor
    /// </summary>
    public void AddDevice(string serverIP, string busId, bool autoReconnect = true, string? receiveMode = null)
    {
        lock (_lock)
        {
            var existing = _monitoredDevices.FirstOrDefault(d => d.ServerIP == serverIP && d.BusId == busId);
            if (existing != null)
            {
                if (receiveMode != null)
                    existing.ReceiveMode = receiveMode;
                return;
            }

            _monitoredDevices.Add(new MonitoredDevice
            {
                ServerIP = serverIP,
                BusId = busId,
                AutoReconnect = autoReconnect,
                ReceiveMode = receiveMode,
                IsConnected = true,
                LastCheck = DateTime.Now
            });
        }
    }

    /// <summary>
    /// Remove device from monitoring
    /// </summary>
    public void RemoveDevice(string serverIP, string busId)
    {
        lock (_lock)
        {
            _monitoredDevices.RemoveAll(d => d.ServerIP == serverIP && d.BusId == busId);
        }
    }

    /// <summary>
    /// Clear all monitored devices
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            _monitoredDevices.Clear();
        }
    }

    private async void OnMonitorTick(object? sender, ElapsedEventArgs e)
    {
        lock (_lock)
        {
            if (!_isRunning || _disposed) return;
        }

        try
        {
            List<MonitoredDevice> devicesToCheck;
            lock (_lock)
            {
                devicesToCheck = _monitoredDevices.ToList();
            }

            if (devicesToCheck.Count > 0)
            {
                var connectedDevices = await _usbip.GetConnectedDevicesAsync();

                foreach (var device in devicesToCheck)
                {
                    var isNowConnected = connectedDevices.Any(c =>
                        c.ServerIP == device.ServerIP &&
                        c.BusId == device.BusId);

                    bool fireLost = false;
                    bool fireRestored = false;
                    bool shouldReconnect = false;

                    lock (_lock)
                    {
                        if (!_isRunning || _disposed || !_monitoredDevices.Contains(device))
                            continue;

                        device.LastCheck = DateTime.Now;

                        // Transition: Connected -> Disconnected
                        if (device.IsConnected && !isNowConnected)
                        {
                            device.IsConnected = false;
                            device.ReconnectAttempts = 0;
                            device.LastReconnectAttemptUtc = DateTime.MinValue;
                            fireLost = true;
                            shouldReconnect = ShouldAttemptReconnect(device, DateTime.UtcNow);
                        }
                        // Still disconnected: keep watching for transient drop or physical replug
                        else if (!device.IsConnected && !isNowConnected)
                        {
                            shouldReconnect = ShouldAttemptReconnect(device, DateTime.UtcNow);
                        }
                        // Transition: Disconnected -> Connected externally
                        else if (!device.IsConnected && isNowConnected)
                        {
                            device.IsConnected = true;
                            device.ReconnectAttempts = 0;
                            device.LastReconnectAttemptUtc = DateTime.MinValue;
                            fireRestored = true;
                        }
                    }

                    if (fireLost)
                    {
                        ConnectionLost?.Invoke(this, new ConnectionLostEventArgs
                        {
                            ServerIP = device.ServerIP,
                            BusId = device.BusId
                        });
                    }

                    if (shouldReconnect)
                    {
                        await TryReconnect(device);
                    }

                    if (fireRestored)
                    {
                        ConnectionRestored?.Invoke(this, new ConnectionRestoredEventArgs
                        {
                            ServerIP = device.ServerIP,
                            BusId = device.BusId
                        });
                    }
                }
            }
        }
        catch (Exception dbgEx)
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionMonitorService] {dbgEx.Message}");
        }
        finally
        {
            lock (_lock)
            {
                if (_isRunning && !_disposed)
                {
                    try { _monitorTimer.Start(); } catch (ObjectDisposedException) { }
                }
            }
        }
    }

    internal static bool ShouldAttemptReconnect(MonitoredDevice device, DateTime nowUtc)
    {
        if (!device.AutoReconnect || device.IsReconnecting)
            return false;

        if (device.LastReconnectAttemptUtc == DateTime.MinValue)
            return true;

        double requiredSeconds = device.ReconnectAttempts < 3
            ? 2.0
            : Math.Min(15.0, 4.0 + (device.ReconnectAttempts - 3) * 2.0);

        return (nowUtc - device.LastReconnectAttemptUtc).TotalSeconds >= requiredSeconds;
    }

    private async Task TryReconnect(MonitoredDevice device)
    {
        int attemptNumber;
        lock (_lock)
        {
            if (!_isRunning || _disposed || !_monitoredDevices.Contains(device) || device.IsReconnecting)
                return;
            device.IsReconnecting = true;
            device.LastReconnectAttemptUtc = DateTime.UtcNow;
            device.ReconnectAttempts++;
            attemptNumber = device.ReconnectAttempts;
        }

        try
        {
            // After initial attempt fails (e.g. physical unplug or cleanup wait),
            // verify the device actually exists on the server before issuing BIND + attach.
            if (attemptNumber > 1)
            {
                var remoteDevices = await _usbip.ListDevicesAsync(device.ServerIP);
                var target = remoteDevices.FirstOrDefault(d =>
                    string.Equals(d.BusId, device.BusId, StringComparison.OrdinalIgnoreCase));
                if (target == null || target.IsConnected)
                {
                    return;
                }
            }

            var result = await _usbip.ConnectAsync(device.ServerIP, device.BusId, device.ReceiveMode);
            if (result.Success)
            {
                bool fireRestored = false;
                lock (_lock)
                {
                    if (_monitoredDevices.Contains(device))
                    {
                        device.IsConnected = true;
                        device.ReconnectAttempts = 0;
                        device.LastReconnectAttemptUtc = DateTime.MinValue;
                        fireRestored = true;
                    }
                }

                if (fireRestored)
                {
                    ConnectionRestored?.Invoke(this, new ConnectionRestoredEventArgs
                    {
                        ServerIP = device.ServerIP,
                        BusId = device.BusId
                    });
                }
            }
        }
        catch (Exception dbgEx)
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionMonitorService] {dbgEx.Message}");
        }
        finally
        {
            lock (_lock)
            {
                device.IsReconnecting = false;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _isRunning = false;
        }
        try { _monitorTimer.Stop(); } catch { }
        _monitorTimer.Dispose();
    }
}

internal class MonitoredDevice
{
    public string ServerIP { get; set; } = "";
    public string BusId { get; set; } = "";
    public string? ReceiveMode { get; set; }
    public bool IsConnected { get; set; }
    public bool AutoReconnect { get; set; }
    public int ReconnectAttempts { get; set; }
    public DateTime LastCheck { get; set; }
    public DateTime LastReconnectAttemptUtc { get; set; } = DateTime.MinValue;
    public bool IsReconnecting { get; set; }
}
