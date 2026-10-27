using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ListViewItem = System.Windows.Controls.ListViewItem;
using USBAnywhere.Models;
using USBAnywhere.Services;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Views;

public partial class VpnDialog : Window
{
    private readonly ILocalizationService _localization;
    private readonly string _language;
    private readonly IVpnService _vpn;
    private List<VpnPeer> _discoveredPeers = new();

    public List<string> SelectedPeerIPs { get; } = new();

    public VpnDialog(ILocalizationService localization, string language, IVpnService? vpnService = null)
    {
        InitializeComponent();

        _localization = localization;
        _language = language;
        _vpn = vpnService ?? new VpnService();

        lstPeers.MouseDoubleClick += (s, e) => BtnConnect_Click(s, new RoutedEventArgs());

        UpdateTexts();
        CheckVpnStatus();
    }

    private void UpdateTexts()
    {
        Title = _localization.GetText("vpn_title", _language);
        lblStatus.Text = _localization.GetText("vpn_status", _language);
        lblPeers.Text = _localization.GetText("vpn_peers", _language);
        btnAddAll.Content = _localization.GetText("vpn_add_all", _language);
        btnConnect.Content = _localization.GetText("vpn_connect", _language);
        lblInfo.Text = _localization.GetText("vpn_info", _language);
    }

    private async void CheckVpnStatus()
    {
        lblStatusValue.Text = _localization.GetText("vpn_checking", _language);

        string? activeVpn = null;

        await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "tailscale",
                    Arguments = "status --json",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        activeVpn = "Tailscale";
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[VpnDialog] Tailscale check: {ex.Message}"); }

            if (activeVpn == null)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "zerotier-cli",
                        Arguments = "status",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        if (output.Contains("ONLINE", StringComparison.OrdinalIgnoreCase))
                        {
                            activeVpn = "ZeroTier";
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[VpnDialog] ZeroTier check: {ex.Message}"); }
            }
        });

        if (activeVpn != null)
        {
            lblStatusValue.Text = $"{activeVpn} {_localization.GetText("vpn_online", _language)}";
            lblStatusValue.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(78, 201, 176));
        }
        else
        {
            lblStatusValue.Text = _localization.GetText("vpn_offline", _language);
            lblStatusValue.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 100, 100));
        }

        try
        {
            var peers = await _vpn.GetAllPeersAsync();
            _discoveredPeers = peers.Where(p => !string.IsNullOrWhiteSpace(p.IpAddress)).ToList();

            lstPeers.Items.Clear();
            if (_discoveredPeers.Count > 0)
            {
                foreach (var peer in _discoveredPeers)
                {
                    var icon = peer.IsOnline ? "🟢" : "⚪";
                    var source = string.IsNullOrEmpty(peer.Source) ? "" : $" [{peer.Source}]";
                    var item = new ListViewItem
                    {
                        Content = $"{icon} {peer.Hostname} — {peer.IpAddress}{source}",
                        Tag = peer.IpAddress
                    };
                    lstPeers.Items.Add(item);
                }
                lstPeers.SelectedIndex = 0;
            }
            else
            {
                lstPeers.Items.Add(new ListViewItem
                {
                    Content = _localization.GetText("vpn_no_peers", _language),
                    IsEnabled = false
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VpnDialog] Peer load error: {ex.Message}");
        }
    }

    private void BtnTailscale_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://tailscale.com/download",
            UseShellExecute = true
        });
    }

    private void BtnZeroTier_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.zerotier.com/download/",
            UseShellExecute = true
        });
    }

    private void BtnAddAll_Click(object sender, RoutedEventArgs e)
    {
        SelectedPeerIPs.Clear();
        foreach (var peer in _discoveredPeers.Where(p => !string.IsNullOrWhiteSpace(p.IpAddress)))
        {
            if (!SelectedPeerIPs.Contains(peer.IpAddress))
                SelectedPeerIPs.Add(peer.IpAddress);
        }

        if (SelectedPeerIPs.Count == 0)
        {
            foreach (var obj in lstPeers.Items)
            {
                if (obj is ListViewItem lvi && lvi.Tag is string tagIp && !string.IsNullOrWhiteSpace(tagIp))
                {
                    if (!SelectedPeerIPs.Contains(tagIp))
                        SelectedPeerIPs.Add(tagIp);
                }
                else if (obj is string str)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(str, @"(\d+\.\d+\.\d+\.\d+)");
                    if (match.Success && !SelectedPeerIPs.Contains(match.Groups[1].Value))
                        SelectedPeerIPs.Add(match.Groups[1].Value);
                }
            }
        }

        Close();
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        SelectedPeerIPs.Clear();
        if (lstPeers.SelectedItem is ListViewItem lvi && lvi.Tag is string ip && !string.IsNullOrWhiteSpace(ip))
        {
            SelectedPeerIPs.Add(ip);
        }
        else if (lstPeers.SelectedItem is string str)
        {
            var match = System.Text.RegularExpressions.Regex.Match(str, @"(\d+\.\d+\.\d+\.\d+)");
            if (match.Success)
                SelectedPeerIPs.Add(match.Groups[1].Value);
        }
        Close();
    }
}
