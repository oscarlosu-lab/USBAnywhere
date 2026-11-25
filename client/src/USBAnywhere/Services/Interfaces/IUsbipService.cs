// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

using USBAnywhere.Models;

namespace USBAnywhere.Services.Interfaces;

public interface IUsbipService
{
    Task<List<UsbDevice>> ListRemoteDevicesAsync(string ip, CancellationToken ct = default);
    Task<bool> AttachDeviceAsync(string ip, string busId, string? receiveMode = null, CancellationToken ct = default, string? serial = null, bool attachOnce = true);
    Task<bool> DetachDeviceAsync(string port, CancellationToken ct = default);
    Task<int> DetachAllConnectedDevicesAsync(CancellationToken ct = default);

    // Compatibility aliases used by MainWindow (delegated internally)
    Task<List<UsbDevice>> ListDevicesAsync(string serverIP);
    Task<OperationResult> ConnectAsync(string serverIP, string busId, string? receiveMode = null, string? serial = null, bool attachOnce = true);
    Task<OperationResult> DisconnectAsync(int port);
    Task<List<ConnectedDevice>> GetConnectedDevicesAsync();
    Task<List<VhciPersistentDevice>> GetKernelPersistentDevicesAsync();
    Task<OperationResult> SetKernelPersistentDevicesAsync(IReadOnlyList<VhciPersistentDevice> devices);
    Task<int> StopKernelAttachAttemptsAsync(string? host = null, string? busId = null);
}


