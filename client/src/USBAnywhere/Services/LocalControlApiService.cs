using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using USBAnywhere.Models;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// Loopback-only HTTP/JSON REST control API (127.0.0.1) for Home Assistant, Stream Deck, and local automation.
/// </summary>
public sealed class LocalControlApiService : IDisposable
{
    private readonly IConfigService _config;
    private readonly IUsbipService _usbip;
    private readonly IDriverService _driver;
    private readonly IFavoritesService _favorites;
    private readonly StreamingSessionMonitor? _streamMonitor;
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public int Port => (_listener?.LocalEndpoint as IPEndPoint)?.Port ?? _port;

    public LocalControlApiService(
        IConfigService config,
        IUsbipService usbip,
        IDriverService driver,
        IFavoritesService favorites,
        StreamingSessionMonitor? streamMonitor = null,
        int port = 13241)
    {
        _config = config;
        _usbip = usbip;
        _driver = driver;
        _favorites = favorites;
        _streamMonitor = streamMonitor;
        _port = port;
    }

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.ExclusiveAddressUse = true;
        _listener.Start();
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleHttpAsync(client, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch { if (ct.IsCancellationRequested) break; }
        }
    }

    private async Task HandleHttpAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(requestLine)) return;

                var reqParts = requestLine.Split(' ', 3);
                if (reqParts.Length < 2) return;
                var method = reqParts[0].ToUpperInvariant();
                var rawTarget = reqParts[1];
                var qIdx = rawTarget.IndexOf('?');
                var path = qIdx >= 0 ? rawTarget[..qIdx] : rawTarget;
                var query = qIdx >= 0 ? rawTarget[(qIdx + 1)..] : "";

                int contentLength = 0;
                string? authHeader = null;
                while (true)
                {
                    var hdr = await reader.ReadLineAsync(ct);
                    if (string.IsNullOrEmpty(hdr)) break;
                    var colon = hdr.IndexOf(':');
                    if (colon <= 0) continue;
                    var name = hdr[..colon].Trim();
                    var val = hdr[(colon + 1)..].Trim();
                    if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out var cl))
                        contentLength = Math.Clamp(cl, 0, 65536);
                    else if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                        authHeader = val;
                }

                var cfg = _config.Load();
                if (!string.IsNullOrWhiteSpace(cfg.LocalApiToken))
                {
                    var expected = $"Bearer {cfg.LocalApiToken.Trim()}";
                    if (authHeader == null ||
                        authHeader.Length != expected.Length ||
                        !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(authHeader), Encoding.UTF8.GetBytes(expected)))
                    {
                        await WriteHttpJsonAsync(stream, 401, new { error = "Unauthorized" }, ct);
                        return;
                    }
                }

                string body = "";
                if (contentLength > 0)
                {
                    var buf = new char[contentLength];
                    int read = 0;
                    while (read < contentLength)
                    {
                        int n = await reader.ReadAsync(buf, read, contentLength - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    body = new string(buf, 0, read);
                }

                if (method == "GET" && path == "/api/v1/status")
                {
                    var connected = await _usbip.GetConnectedDevicesAsync();
                    var installed = await _driver.IsDriverInstalledAsync();
                    await WriteHttpJsonAsync(stream, 200, new
                    {
                        status = "ok",
                        driverInstalled = installed,
                        driverStatus = _driver.GetDriverStatus(),
                        connectedCount = connected.Count,
                        favoritesCount = cfg.Favorites?.Count ?? 0,
                        streamActive = _streamMonitor?.IsStreamActive ?? false,
                        streamProvider = _streamMonitor?.ActiveProvider ?? ""
                    }, ct);
                }
                else if (method == "GET" && path == "/api/v1/doctor")
                {
                    var connected = await _usbip.GetConnectedDevicesAsync();
                    var installed = await _driver.IsDriverInstalledAsync();
                    await WriteHttpJsonAsync(stream, 200, new
                    {
                        healthy = installed,
                        driverInstalled = installed,
                        driverStatus = _driver.GetDriverStatus(),
                        connectedCount = connected.Count,
                        autoDetachOnSleep = cfg.AutoDetachOnSleep
                    }, ct);
                }
                else if (method == "GET" && path == "/api/v1/connected")
                {
                    var connected = await _usbip.GetConnectedDevicesAsync();
                    await WriteHttpJsonAsync(stream, 200, new { devices = connected }, ct);
                }
                else if (method == "GET" && path == "/api/v1/favorites")
                {
                    await WriteHttpJsonAsync(stream, 200, new { favorites = _favorites.GetFavorites() }, ct);
                }
                else if (method == "GET" && path == "/api/v1/devices")
                {
                    var server = query.Split('&').Select(p => p.Split('=', 2))
                        .FirstOrDefault(kv => kv.Length == 2 && kv[0] == "server")?[1] ?? "";
                    server = Uri.UnescapeDataString(server);
                    if (!Utils.InputValidator.IsValidHost(server))
                    {
                        await WriteHttpJsonAsync(stream, 400, new { error = "Invalid server parameter" }, ct);
                        return;
                    }
                    var devs = await _usbip.ListRemoteDevicesAsync(server, ct);
                    await WriteHttpJsonAsync(stream, 200, new { server, devices = devs }, ct);
                }
                else if (method == "POST" && path == "/api/v1/attach")
                {
                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                    var r = doc.RootElement;
                    var serverIp = r.TryGetProperty("serverIp", out var sEl) ? sEl.GetString() ?? "" : "";
                    var busId = r.TryGetProperty("busId", out var bEl) ? bEl.GetString() ?? "" : "";
                    var mode = r.TryGetProperty("mode", out var mEl) ? mEl.GetString() : null;
                    if (!Utils.InputValidator.IsValidHost(serverIp) || !Utils.InputValidator.IsValidBusId(busId))
                    {
                        await WriteHttpJsonAsync(stream, 400, new { error = "Invalid serverIp or busId" }, ct);
                        return;
                    }
                    var op = await _usbip.ConnectAsync(serverIp, busId, mode);
                    await WriteHttpJsonAsync(stream, op.Success ? 200 : 502, op, ct);
                }
                else if (method == "POST" && path == "/api/v1/detach")
                {
                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                    var r = doc.RootElement;
                    if (r.TryGetProperty("all", out var allEl) && allEl.ValueKind == JsonValueKind.True)
                    {
                        int count = await _usbip.DetachAllConnectedDevicesAsync(ct);
                        await WriteHttpJsonAsync(stream, 200, new { detached = count }, ct);
                    }
                    else if (r.TryGetProperty("port", out var pEl) && pEl.TryGetInt32(out int portNum))
                    {
                        var op = await _usbip.DisconnectAsync(portNum);
                        await WriteHttpJsonAsync(stream, op.Success ? 200 : 400, op, ct);
                    }
                    else
                    {
                        await WriteHttpJsonAsync(stream, 400, new { error = "Provide 'port' or 'all': true" }, ct);
                    }
                }
                else if (method == "POST" && path == "/api/v1/stream")
                {
                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                    var r = doc.RootElement;
                    bool active = r.TryGetProperty("active", out var aEl) && aEl.ValueKind == JsonValueKind.True;
                    string provider = r.TryGetProperty("provider", out var pEl) ? (pEl.GetString() ?? "sunshine") : "sunshine";
                    _streamMonitor?.SetStreamState(active, provider);
                    await WriteHttpJsonAsync(stream, 200, new { status = "ok", active, provider }, ct);
                }
                else
                {
                    await WriteHttpJsonAsync(stream, 404, new { error = "Not found" }, ct);
                }
            }
            catch
            {
                // Ignore client socket teardown
            }
        }
    }

    private static async Task WriteHttpJsonAsync(NetworkStream stream, int code, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        var bodyBytes = Encoding.UTF8.GetBytes(json);
        var statusText = code switch { 200 => "OK", 400 => "Bad Request", 401 => "Unauthorized", 404 => "Not Found", _ => "Error" };
        var header = $"HTTP/1.1 {code} {statusText}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, ct);
        await stream.WriteAsync(bodyBytes, ct);
        await stream.FlushAsync(ct);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        _listener = null;
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
