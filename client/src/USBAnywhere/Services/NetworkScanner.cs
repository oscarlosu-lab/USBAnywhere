// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

using System.Net.Sockets;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// Network scanner - Parallel subnet scanning for USBIP servers
/// Part of USBAnywhere by SnakeFoxu (OscarLosu)
/// </summary>
public class NetworkScanner : INetworkScanner
{
    private const int UsbipPort = 3240;
    private const int FirstTimeoutMs = 500;   // Fast first attempt
    private const int RetryTimeoutMs = 1500;  // Slower retry for embedded

    public async Task<List<string>> ScanNetworkAsync(string subnetBase)
    {
        var servers = new List<string>();
        var tasks = new List<Task<string?>>(254);

        // Scan 1-254 in parallel
        for (int i = 1; i <= 254; i++)
        {
            var ip = $"{subnetBase}.{i}";
            tasks.Add(CheckServerWithRetryAsync(ip));
        }

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            if (!string.IsNullOrEmpty(result))
                servers.Add(result);
        }

        return servers;
    }

    private async Task<string?> CheckServerWithRetryAsync(string ip)
    {
        // First attempt - fast timeout
        var result = await CheckServerAsync(ip, FirstTimeoutMs);
        if (result != null) return result;

        // Retry with longer timeout for slow embedded devices (CrealityBox, etc.)
        return await CheckServerAsync(ip, RetryTimeoutMs);
    }

    private static async Task<string?> CheckServerAsync(string ip, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(ip, UsbipPort, cts.Token);
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
