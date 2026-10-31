// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

using System.Collections.ObjectModel;
using System.Windows.Input;
using USBAnywhere.Models;
using USBAnywhere.Services.Interfaces;
using USBAnywhere.Utils;

namespace USBAnywhere.ViewModels;

/// <summary>
/// Main application ViewModel — used by VPN panel and future MVVM refactor
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IVpnService _vpn;
    private readonly IUsbipService _usbip;

    public ObservableCollection<VpnPeer> VpnPeers { get; } = new();

    private bool _isLoadingVpn;
    public bool IsLoadingVpn
    {
        get => _isLoadingVpn;
        set => SetField(ref _isLoadingVpn, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ConnectVpnCommand { get; }

    public MainViewModel(IVpnService vpn, IUsbipService usbip)
    {
        _vpn   = vpn;
        _usbip = usbip;

        RefreshCommand    = new RelayCommand(async _ => await RefreshAllAsync());
        ConnectVpnCommand = new RelayCommand<VpnPeer>(async peer => await ConnectVpnAsync(peer));
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    private async Task RefreshAllAsync()
    {
        IsLoadingVpn  = true;
        StatusMessage = "Scanning VPN peers...";
        VpnPeers.Clear();

        try
        {
            var peers = await _vpn.GetAllPeersAsync();
            foreach (var peer in peers)
                VpnPeers.Add(peer);

            StatusMessage = VpnPeers.Count > 0
                ? $"Found {VpnPeers.Count} VPN peer(s)"
                : "No VPN peers found";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoadingVpn = false;
        }
    }

    private async Task ConnectVpnAsync(VpnPeer? peer)
    {
        if (peer == null) return;
        StatusMessage = $"Connecting to {peer.Hostname}...";
        var ok = await _vpn.ConnectAsync(peer.IpAddress);
        StatusMessage = ok ? $"Connected to {peer.Hostname}" : $"Failed to connect to {peer.Hostname}";
    }
}
