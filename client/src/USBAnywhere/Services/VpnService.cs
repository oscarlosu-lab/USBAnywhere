using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using USBAnywhere.Models;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services
{
    /// <summary>
    /// VPN service - Detect Tailscale/ZeroTier/WireGuard and enumerate peers
    /// </summary>
    public class VpnService : IVpnService
    {
        private string _currentConfig = string.Empty;

        public bool IsAvailable => IsTailscaleInstalled() || IsZeroTierInstalled() || IsWireGuardInstalled();
        public bool IsConnected => !string.IsNullOrEmpty(_currentConfig) || IsAvailable;
        public string CurrentConfig => _currentConfig;

        internal static string? ResolveSafeVpnBinary(params string[] relativePaths)
        {
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (var root in roots)
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                foreach (var rel in relativePaths)
                {
                    var candidate = Path.Combine(root, rel);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            return null;
        }

        internal static string? GetTailscaleExePath() =>
            ResolveSafeVpnBinary(Path.Combine("Tailscale", "tailscale.exe"));

        internal static string? GetZeroTierExePath() =>
            ResolveSafeVpnBinary(
                Path.Combine("ZeroTier", "One", "zerotier-cli.bat"),
                Path.Combine("ZeroTier", "One", "zerotier-cli.exe"));

        internal static string? GetWireGuardExePath() =>
            ResolveSafeVpnBinary(Path.Combine("WireGuard", "wg.exe"));

        public bool IsTailscaleInstalled()
        {
            var exePath = GetTailscaleExePath();
            return exePath != null && File.Exists(exePath);
        }

        public bool IsZeroTierInstalled()
        {
            var exePath = GetZeroTierExePath();
            return exePath != null && File.Exists(exePath);
        }

        public bool IsWireGuardInstalled()
        {
            var exePath = GetWireGuardExePath();
            return exePath != null && File.Exists(exePath);
        }

        public async Task<IEnumerable<VpnPeer>> GetTailscalePeersAsync()
        {
            var peers = new List<VpnPeer>();
            var exePath = GetTailscaleExePath();
            if (exePath == null || !File.Exists(exePath)) return peers;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "status --json",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return peers;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                string output;
                try
                {
                    output = await process.StandardOutput.ReadToEndAsync(cts.Token);
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); } catch { }
                    return peers;
                }

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    using var doc = JsonDocument.Parse(output);
                    if (doc.RootElement.TryGetProperty("Peer", out var peerElement) &&
                        peerElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var peer in peerElement.EnumerateObject())
                        {
                            var peerData = peer.Value;
                            var hostName = peerData.TryGetProperty("HostName", out var hn) && hn.ValueKind == JsonValueKind.String
                                ? hn.GetString() ?? "Unknown"
                                : "Unknown";

                            string ipAddress = string.Empty;
                            if (peerData.TryGetProperty("TailscaleIPs", out var tsIps) && tsIps.ValueKind == JsonValueKind.Array)
                            {
                                ipAddress = tsIps.EnumerateArray()
                                    .Select(ip => ip.ValueKind == JsonValueKind.String ? ip.GetString() : null)
                                    .FirstOrDefault(ip => !string.IsNullOrEmpty(ip)) ?? string.Empty;
                            }

                            var isOnline = peerData.TryGetProperty("Online", out var onEl) &&
                                           (onEl.ValueKind == JsonValueKind.True || onEl.ValueKind == JsonValueKind.False) &&
                                           onEl.GetBoolean();

                            if (!string.IsNullOrEmpty(ipAddress))
                            {
                                peers.Add(new VpnPeer
                                {
                                    Hostname = hostName,
                                    IpAddress = ipAddress,
                                    IsOnline = isOnline,
                                    Source = "Tailscale",
                                    LastSeen = DateTime.Now
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception dbgEx) { System.Diagnostics.Debug.WriteLine($"[VpnService] {dbgEx.Message}"); }
            return peers;
        }

        public async Task<IEnumerable<VpnPeer>> GetZeroTierPeersAsync()
        {
            var peers = new List<VpnPeer>();
            var exePath = GetZeroTierExePath();
            if (exePath == null || !File.Exists(exePath)) return peers;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "peers -j",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return peers;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                string output;
                try
                {
                    output = await process.StandardOutput.ReadToEndAsync(cts.Token);
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); } catch { }
                    return peers;
                }

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    using var doc = JsonDocument.Parse(output);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var peer in doc.RootElement.EnumerateArray())
                        {
                            var address = peer.TryGetProperty("address", out var addrEl) ? addrEl.GetString() : "peer";
                            var ip = string.Empty;

                            if (peer.TryGetProperty("paths", out var paths) &&
                                paths.ValueKind == JsonValueKind.Array &&
                                paths.GetArrayLength() > 0)
                            {
                                if (paths[0].TryGetProperty("address", out var pAddr) && pAddr.GetString() is string firstPath)
                                {
                                    ip = firstPath.Split('/')[0];
                                }
                            }

                            var role = peer.TryGetProperty("role", out var roleEl) ? roleEl.GetString() : "UNKNOWN";
                            if (!string.IsNullOrEmpty(ip))
                            {
                                peers.Add(new VpnPeer
                                {
                                    Hostname = $"ZT-{address}",
                                    IpAddress = ip,
                                    IsOnline = role != "UNKNOWN",
                                    Source = "ZeroTier",
                                    LastSeen = DateTime.Now
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception dbgEx) { System.Diagnostics.Debug.WriteLine($"[VpnService] {dbgEx.Message}"); }
            return peers;
        }

        public async Task<IEnumerable<VpnPeer>> GetWireGuardPeersAsync()
        {
            var peers = new List<VpnPeer>();
            var exePath = GetWireGuardExePath();
            if (exePath == null || !File.Exists(exePath)) return peers;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "show all dump",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return peers;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                string output;
                try
                {
                    output = await process.StandardOutput.ReadToEndAsync(cts.Token);
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); } catch { }
                    return peers;
                }

                if (!string.IsNullOrEmpty(output))
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('\t');
                        if (parts.Length >= 4)
                        {
                            var endpoint = parts.FirstOrDefault(p => p.Contains(':'));
                            if (endpoint != null)
                            {
                                var lastColon = endpoint.LastIndexOf(':');
                                var ip = (lastColon > 0 ? endpoint[..lastColon] : endpoint).Trim('[', ']');
                                var shortKey = parts[1].Length >= 8 ? parts[1][..8] : parts[1];
                                peers.Add(new VpnPeer
                                {
                                    Hostname = $"WG-{shortKey}",
                                    IpAddress = ip,
                                    IsOnline = true,
                                    Source = "WireGuard",
                                    LastSeen = DateTime.Now
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception dbgEx) { System.Diagnostics.Debug.WriteLine($"[VpnService] {dbgEx.Message}"); }
            return peers;
        }

        public async Task<IEnumerable<VpnPeer>> GetAllPeersAsync()
        {
            var allPeers = new List<VpnPeer>();

            if (IsTailscaleInstalled())
                allPeers.AddRange(await GetTailscalePeersAsync());

            if (IsZeroTierInstalled())
                allPeers.AddRange(await GetZeroTierPeersAsync());

            if (IsWireGuardInstalled())
                allPeers.AddRange(await GetWireGuardPeersAsync());

            return allPeers;
        }

        public Task<bool> ConnectAsync(string configPath, CancellationToken ct = default)
        {
            _currentConfig = configPath;
            return Task.FromResult(true);
        }

        public Task<bool> DisconnectAsync(CancellationToken ct = default)
        {
            _currentConfig = string.Empty;
            return Task.FromResult(true);
        }

        public async Task<bool> TestUSBIPOnPeerAsync(string ip)
        {
            try
            {
                using var tcp = new TcpClient();
                using var cts = new CancellationTokenSource(1000);
                await tcp.ConnectAsync(ip, 3240, cts.Token);
                return tcp.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
}
