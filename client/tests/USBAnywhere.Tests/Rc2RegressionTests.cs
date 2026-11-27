using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using USBAnywhere.Models;
using USBAnywhere.Services;
using USBAnywhere.Services.Interfaces;
using Xunit;

namespace USBAnywhere.Tests;

public class Rc2RegressionTests
{
    private sealed class StubConfigService : IConfigService
    {
        public AppConfig Config { get; set; } = new();
        public AppConfig Load() => Config;
        public void Save(AppConfig config) => Config = config;
        public void SetLanguage(string language) => Config.Language = language;
    }

    private sealed class StubEnv : IEnvironmentService
    {
        public string AppDataPath => Path.GetTempPath();
        public string LogsPath => Path.GetTempPath();
        public string BinPath => Path.GetTempPath();
        public string DriversPath => Path.GetTempPath();
        public string TempPath => Path.GetTempPath();
        public bool IsPortableMode => false;
        public bool IsPortable => false;
        public string GetConfigFilePath() => Path.Combine(Path.GetTempPath(), "cfg.json");
        public void Initialize() { }
    }

    private sealed class StubDriver : IDriverService
    {
        public Task<bool> IsDriverInstalledAsync() => Task.FromResult(true);
        public string GetDriverStatus() => "Installed";
        public Task<bool> InstallDriverAsync() => Task.FromResult(true);
        public Task<bool> UninstallDriverAsync() => Task.FromResult(true);
    }

    [Fact]
    public async Task LocalControlApi_Returns400_OnMalformedJson()
    {
        var cfg = new StubConfigService();
        cfg.Config.LocalApiToken = "";
        var usbip = new UsbipService(NullLogger<UsbipService>.Instance, new StubEnv(), new StubDriver(), null, cfg);
        var favs = new FavoritesService(cfg, usbip);

        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        using var api = new LocalControlApiService(cfg, usbip, new StubDriver(), favs, null, port);
        api.Start();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var resp = await http.PostAsync($"http://127.0.0.1:{port}/api/v1/attach",
            new StringContent("{invalid-json", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public void FormatControlCommand_IncludesToken_WhenPairedServerHasToken()
    {
        var cfg = new StubConfigService();
        cfg.Config.PairedServers.Add(new PairedServerEntry
        {
            Host = "10.0.0.5",
            ControlToken = "deadbeef1234"
        });
        var usbip = new UsbipService(NullLogger<UsbipService>.Instance, new StubEnv(), new StubDriver(), null, cfg);

        Assert.Equal("TOKEN deadbeef1234 BIND 2-1\n", usbip.FormatControlCommand("10.0.0.5", "BIND 2-1"));
        Assert.Equal("BIND 2-1\n", usbip.FormatControlCommand("10.0.0.99", "BIND 2-1"));
    }

    [Fact]
    public async Task PairingClientService_AcceptsControlTokenAndServerNameAliases()
    {
        var cfg = new StubConfigService();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var acceptTask = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync();
            using var s = c.GetStream();
            using var r = new StreamReader(s, Encoding.UTF8, leaveOpen: true);
            _ = await r.ReadLineAsync();
            var json = "{\"status\":\"OK\",\"server_name\":\"AndroidHost\",\"control_token\":\"tok999\",\"tls_port\":3243,\"tls_fingerprint\":\"\"}\n";
            var b = Encoding.UTF8.GetBytes(json);
            await s.WriteAsync(b);
            await s.FlushAsync();
        });

        var client = new PairingClientService(cfg);
        var res = await client.PairWithServerAsync("127.0.0.1", "123456", port);
        await acceptTask;

        Assert.True(res.Success);
        Assert.NotNull(res.Entry);
        Assert.Equal("AndroidHost", res.Entry!.ServerName);
        Assert.Equal("tok999", res.Entry.ControlToken);
    }

    [Fact]
    public void IsHeadlessCliInvocation_DetectsStatusAndDoctor()
    {
        Assert.True(App.IsHeadlessCliInvocation(new[] { "--status", "--json" }));
        Assert.True(App.IsHeadlessCliInvocation(new[] { "--doctor" }));
        Assert.False(App.IsHeadlessCliInvocation(Array.Empty<string>()));
    }
}
