// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

using System.Net;
using System.Net.Sockets;
using System.Text;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// Network scanner - Parallel subnet scanning (TCP 3240 + Dynamic Control TCP/UDP 3241)
/// Part of USBAnywhere by SnakeFoxu (OscarLosu)
/// </summary>
public class NetworkScanner : INetworkScanner
{
    private const int UsbipPort = 3240;
    private const int ControlPort = 3241;
    private const int FirstTimeoutMs = 500;   // Fast first attempt
    private const int RetryTimeoutMs = 1200;  // Slower retry for embedded

    public async Task<List<string>> ScanNetworkAsync(string subnetBase)
    {
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Fast UDP 3241 broadcast probe in parallel with TCP scan
        var udpTask = ProbeUdpBroadcastAsync(subnetBase, 600);

        var tasks = new List<Task<string?>>(254);
        for (int i = 1; i <= 254; i++)
        {
            var ip = $"{subnetBase}.{i}";
            tasks.Add(CheckServerWithRetryAsync(ip));
        }

        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
        {
            if (!string.IsNullOrEmpty(result))
                discovered.Add(result);
        }

        var udpServers = await udpTask;
        foreach (var srv in udpServers)
        {
            discovered.Add(srv);
        }

        return discovered.OrderBy(ip =>
        {
            var lastDot = ip.LastIndexOf('.');
            return lastDot >= 0 && int.TryParse(ip[(lastDot + 1)..], out int hostNum) ? hostNum : 999;
        }).ToList();
    }

    private static async Task<List<string>> ProbeUdpBroadcastAsync(string subnetBase, int timeoutMs)
    {
        var found = new List<string>();
        try
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            var probe = Encoding.UTF8.GetBytes("USBANYWHERE_DISCOVERY_PROBE");

            var subnetBroadcast = new IPEndPoint(IPAddress.Parse($"{subnetBase}.255"), ControlPort);
            await udp.SendAsync(probe, probe.Length, subnetBroadcast);

            using var cts = new CancellationTokenSource(timeoutMs);
            while (!cts.IsCancellationRequested)
            {
                var res = await udp.ReceiveAsync(cts.Token);
                var text = Encoding.UTF8.GetString(res.Buffer);
                if (text.StartsWith("USBANYWHERE_SERVER:", StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(res.RemoteEndPoint.Address.ToString());
                }
            }
        }
        catch
        {
            // Expected timeout when UDP collection window completes
        }
        return found;
    }

    private async Task<string?> CheckServerWithRetryAsync(string ip)
    {
        // First attempt - fast timeout on TCP 3240 or 3241
        var result = await CheckServerAsync(ip, UsbipPort, FirstTimeoutMs)
                  ?? await CheckServerAsync(ip, ControlPort, FirstTimeoutMs);
        if (result != null) return result;

        // Retry with longer timeout for slow embedded devices
        return await CheckServerAsync(ip, UsbipPort, RetryTimeoutMs);
    }

    private static async Task<string?> CheckServerAsync(string ip, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(ip, port, cts.Token);
            if (client.Connected)
                return ip;
        }
        catch
        {
            // Expected timeout or connection refused on non-USB/IP hosts
        }

        return null;
    }
}
