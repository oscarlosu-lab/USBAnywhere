using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace USBAnywhere.Services;

/// <summary>
/// Loopback TCP-to-TLS bridge allowing the native USB/IP kernel driver to attach over pinned TLS 1.2/1.3.
/// </summary>
public sealed class TlsTunnelBridge : IDisposable
{
    private readonly string _remoteHost;
    private readonly int _remoteTlsPort;
    private readonly string _expectedFingerprintSha256;
    private TcpListener? _localListener;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public int LocalPort => (_localListener?.LocalEndpoint as IPEndPoint)?.Port ?? 0;

    public TlsTunnelBridge(string remoteHost, int remoteTlsPort, string expectedFingerprintSha256)
    {
        _remoteHost = remoteHost;
        _remoteTlsPort = remoteTlsPort;
        _expectedFingerprintSha256 = expectedFingerprintSha256.Trim().ToLowerInvariant();
    }

    public static bool VerifyCertificateFingerprint(byte[]? certSha256Hash, string expectedHex)
    {
        if (certSha256Hash == null || string.IsNullOrWhiteSpace(expectedHex)) return false;
        var actualHex = Convert.ToHexString(certSha256Hash).ToLowerInvariant();
        var cleanExpected = expectedHex.Trim().ToLowerInvariant();
        if (actualHex.Length != cleanExpected.Length) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actualHex),
            Encoding.ASCII.GetBytes(cleanExpected));
    }

    public int Start()
    {
        if (_cts != null) return LocalPort;
        _cts = new CancellationTokenSource();
        _localListener = new TcpListener(IPAddress.Loopback, 0);
        _localListener.Start();
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
        return LocalPort;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _localListener != null)
        {
            try
            {
                var localClient = await _localListener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => PumpConnectionAsync(localClient, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch { if (ct.IsCancellationRequested) break; }
        }
    }

    private async Task PumpConnectionAsync(TcpClient localClient, CancellationToken ct)
    {
        using (localClient)
        {
            try
            {
                localClient.NoDelay = true;
                using var remoteTcp = new TcpClient();
                remoteTcp.NoDelay = true;
                await remoteTcp.ConnectAsync(_remoteHost, _remoteTlsPort, ct);

                using var ssl = new SslStream(
                    remoteTcp.GetStream(),
                    leaveInnerStreamOpen: false,
                    (_, cert, _, _) =>
                    {
                        if (cert == null) return false;
                        return VerifyCertificateFingerprint(
                            cert.GetCertHash(HashAlgorithmName.SHA256),
                            _expectedFingerprintSha256);
                    });

                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = _remoteHost,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                }, ct);

                using var localStream = localClient.GetStream();
                var t1 = localStream.CopyToAsync(ssl, 65536, ct);
                var t2 = ssl.CopyToAsync(localStream, 65536, ct);
                await Task.WhenAny(t1, t2);
            }
            catch
            {
                // Closed on pin mismatch or upstream drop
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _localListener?.Stop(); } catch { }
        _localListener = null;
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
