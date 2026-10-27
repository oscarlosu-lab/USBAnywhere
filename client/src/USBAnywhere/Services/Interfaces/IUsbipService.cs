// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025 SnakeFoxu - GPL v3

using USBAnywhere.Models;

namespace USBAnywhere.Services.Interfaces;

public interface IUsbipService
{
    Task<List<UsbDevice>> ListRemoteDevicesAsync(string ip, CancellationToken ct = default);
    Task<bool> AttachDeviceAsync(string ip, string busId, string? receiveMode = null, CancellationToken ct = default);
    Task<bool> DetachDeviceAsync(string port, CancellationToken ct = default);
    Task<int> DetachAllConnectedDevicesAsync(CancellationToken ct = default);

    // Compatibility aliases used by MainWindow (delegated internally)
    Task<List<UsbDevice>> ListDevicesAsync(string serverIP);
    Task<OperationResult> ConnectAsync(string serverIP, string busId, string? receiveMode = null);
    Task<OperationResult> DisconnectAsync(int port);
    Task<List<ConnectedDevice>> GetConnectedDevicesAsync();
}


