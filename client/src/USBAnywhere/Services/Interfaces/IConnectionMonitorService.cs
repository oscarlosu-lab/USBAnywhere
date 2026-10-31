// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

using USBAnywhere.Models;

namespace USBAnywhere.Services.Interfaces;

public interface IConnectionMonitorService : IDisposable
{
    event EventHandler<ConnectionLostEventArgs>? ConnectionLost;
    event EventHandler<ConnectionRestoredEventArgs>? ConnectionRestored;

    void Start();
    void Stop();
    void AddDevice(string serverIP, string busId, bool autoReconnect = true);
    void RemoveDevice(string serverIP, string busId);
    void ClearAll();
}

public class ConnectionLostEventArgs : EventArgs
{
    public string ServerIP { get; set; } = string.Empty;
    public string BusId { get; set; } = string.Empty;
}

public class ConnectionRestoredEventArgs : EventArgs
{
    public string ServerIP { get; set; } = string.Empty;
    public string BusId { get; set; } = string.Empty;
}
