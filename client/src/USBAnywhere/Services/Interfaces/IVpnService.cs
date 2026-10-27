// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025 SnakeFoxu - GPL v3

using USBAnywhere.Models;

namespace USBAnywhere.Services.Interfaces;

public interface IVpnService
{
    bool IsAvailable { get; }
    bool IsConnected { get; }
    string CurrentConfig { get; }

    bool IsTailscaleInstalled();
    bool IsZeroTierInstalled();
    bool IsWireGuardInstalled();

    Task<IEnumerable<VpnPeer>> GetTailscalePeersAsync();
    Task<IEnumerable<VpnPeer>> GetZeroTierPeersAsync();
    Task<IEnumerable<VpnPeer>> GetWireGuardPeersAsync();
    Task<IEnumerable<VpnPeer>> GetAllPeersAsync();
    Task<bool> ConnectAsync(string configPath, CancellationToken ct = default);
    Task<bool> DisconnectAsync(CancellationToken ct = default);
    Task<bool> TestUSBIPOnPeerAsync(string ip);
}
