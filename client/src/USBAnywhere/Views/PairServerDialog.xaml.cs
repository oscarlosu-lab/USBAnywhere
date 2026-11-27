using System.Windows;
using System.Windows.Controls;
using USBAnywhere.Services;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Views;

public partial class PairServerDialog : Window
{
    private readonly IConfigService _config;
    public string? PairedHost { get; private set; }

    public PairServerDialog(IConfigService config, string? initialHost = null)
    {
        _config = config;
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(initialHost))
            txtHost.Text = initialHost.Trim();
    }

    private void TxtUa2Token_TextChanged(object sender, TextChangedEventArgs e)
    {
        var parsed = PairingClientService.ParseUa2Token(txtUa2Token.Text);
        if (parsed == null) return;
        txtHost.Text = parsed.Value.Host;
        txtCode.Text = parsed.Value.Code;
        chkUseTls.IsChecked = parsed.Value.TlsPort > 0 && !string.IsNullOrWhiteSpace(parsed.Value.TlsFingerprint);
        txtPairStatus.Text = $"Token UA2 válido para {parsed.Value.ServerName} ({parsed.Value.Host})";
    }

    private async void BtnPairNow_Click(object sender, RoutedEventArgs e)
    {
        btnPairNow.IsEnabled = false;
        txtPairStatus.Text = "Emparejando...";
        try
        {
            var host = txtHost.Text.Trim();
            var code = txtCode.Text.Trim();
            var svc = new PairingClientService(_config);
            var (ok, entry, msg) = await svc.PairWithServerAsync(host, code);
            if (!ok || entry == null)
            {
                txtPairStatus.Text = $"Error: {msg}";
                return;
            }

            if (chkUseTls.IsChecked != true)
            {
                var cfg = _config.Load();
                var saved = cfg.PairedServers.FirstOrDefault(s => string.Equals(s.Host, entry.Host, StringComparison.OrdinalIgnoreCase));
                if (saved != null)
                {
                    saved.UseTls = false;
                    _config.Save(cfg);
                }
            }

            PairedHost = entry.Host;
            var fpPreview = string.IsNullOrEmpty(entry.TlsFingerprint) ? "N/A" : entry.TlsFingerprint[..Math.Min(16, entry.TlsFingerprint.Length)];
            txtPairStatus.Text = $"Emparejado con {entry.ServerName} (SHA-256: {fpPreview}...)";
            DialogResult = true;
            Close();
        }
        finally
        {
            btnPairNow.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
