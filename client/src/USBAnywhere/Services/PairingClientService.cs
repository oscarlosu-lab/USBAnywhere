using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using USBAnywhere.Models;
using USBAnywhere.Services.Interfaces;
using USBAnywhere.Utils;

namespace USBAnywhere.Services;

public sealed class PairingClientService
{
    private readonly IConfigService _config;

    public PairingClientService(IConfigService config)
    {
        _config = config;
    }

    public static (string Host, int UsbipPort, string ServerName, int TlsPort, string Code, string TlsFingerprint)? ParseUa2Token(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var trimmed = token.Trim();
        if (!trimmed.StartsWith("UA2:", StringComparison.Ordinal)) return null;
        try
        {
            var b64 = trimmed[4..].Replace('-', '+').Replace('_', '/');
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
            }
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            var parts = raw.Split('|');
            if (parts.Length < 5) return null;
            var host = parts[0].Trim();
            if (!InputValidator.IsValidHost(host)) return null;
            int usbipPort = int.TryParse(parts[1], out var p) ? p : 3240;
            var name = parts[2].Trim();
            int tlsPort = int.TryParse(parts[3], out var tp) ? tp : 3243;
            var code = parts[4].Trim();
            var fp = parts.Length > 5 ? parts[5].Trim().ToLowerInvariant() : string.Empty;
            return (host, usbipPort, name, tlsPort, code, fp);
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, PairedServerEntry? Entry, string Message)> PairWithServerAsync(
        string host,
        string sixDigitCode,
        int controlPort = 3241,
        CancellationToken ct = default)
    {
        var cleanHost = host.Trim();
        var cleanCode = new string((sixDigitCode ?? "").Where(char.IsDigit).ToArray());
        if (!InputValidator.IsValidHost(cleanHost) || cleanCode.Length != 6)
            return (false, null, "Invalid host or 6-digit pairing code.");

        try
        {
            using var tcp = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(5000);
            await tcp.ConnectAsync(cleanHost, controlPort, cts.Token);
            using var stream = tcp.GetStream();
            var req = Encoding.UTF8.GetBytes($"PAIR {cleanCode} {Environment.MachineName}\n");
            await stream.WriteAsync(req, cts.Token);
            await stream.FlushAsync(cts.Token);

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var line = await reader.ReadLineAsync(cts.Token);
            if (string.IsNullOrWhiteSpace(line))
                return (false, null, "Empty response from server.");

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
            if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                var err = root.TryGetProperty("message", out var m) ? m.GetString() : "Pairing denied";
                return (false, null, err ?? "Pairing denied");
            }

            var entry = new PairedServerEntry
            {
                Host = cleanHost,
                ServerName = root.TryGetProperty("server", out var sv) ? (sv.GetString() ?? cleanHost) : cleanHost,
                ControlToken = root.TryGetProperty("token", out var tk) ? (tk.GetString() ?? string.Empty) : string.Empty,
                TlsPort = root.TryGetProperty("tls_port", out var tp) && tp.TryGetInt32(out var tpVal) ? tpVal : 3243,
                TlsFingerprint = root.TryGetProperty("tls_fingerprint", out var fp) ? (fp.GetString() ?? string.Empty) : string.Empty,
                UseTls = root.TryGetProperty("tls_fingerprint", out var fp2) && !string.IsNullOrWhiteSpace(fp2.GetString()),
                PairedAtUtc = DateTime.UtcNow.ToString("O")
            };

            var cfg = _config.Load();
            cfg.PairedServers ??= new List<PairedServerEntry>();
            cfg.PairedServers.RemoveAll(s => string.Equals(s.Host, cleanHost, StringComparison.OrdinalIgnoreCase));
            cfg.PairedServers.Add(entry);
            _config.Save(cfg);

            return (true, entry, "Paired successfully.");
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}
