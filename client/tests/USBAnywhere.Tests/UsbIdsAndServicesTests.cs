using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using USBAnywhere.Models;
using USBAnywhere.Services;
using USBAnywhere.Services.Interfaces;
using Xunit;

namespace USBAnywhere.Tests;

public class UsbIdsAndServicesTests
{
    [Theory]
    [InlineData("046d", "Logitech")]
    [InlineData("0x046d", "Logitech")]
    [InlineData("46d", "Logitech")]
    [InlineData("0BDA", "Realtek")]
    [InlineData("045e", "Microsoft")]
    [InlineData("05ac", "Apple")]
    public void UsbIdsService_GetVendor_PreservesLeadingZeroHexIds(string vid, string expectedSubstring)
    {
        var svc = new UsbIdsService();
        var vendor = svc.GetVendor(vid);
        Assert.Contains(expectedSubstring, vendor);
        Assert.DoesNotContain("Unknown", vendor);
    }

    [Theory]
    [InlineData("046d", "c52b", "Unifying Receiver")]
    [InlineData("0x046d", "0xc52b", "Unifying Receiver")]
    [InlineData("0bda", "8153", "RTL8153")]
    [InlineData("1a86", "7523", "CH340")]
    public void UsbIdsService_GetProduct_ResolvesLeadingZeroProducts(string vid, string pid, string expectedSubstring)
    {
        var svc = new UsbIdsService();
        var product = svc.GetProduct(vid, pid);
        Assert.Contains(expectedSubstring, product);
        Assert.DoesNotContain("Unknown", product);
    }

    [Fact]
    public void UsbIdsService_CustomNames_OverrideBuiltInDescriptions()
    {
        var svc = new UsbIdsService();
        Assert.Null(svc.GetCustomName("046d", "c52b"));
        svc.SetCustomName("046d", "c52b", "My SimRacing Pedals");
        Assert.Equal("My SimRacing Pedals", svc.GetCustomName("046d", "c52b"));

        Assert.Equal("My SimRacing Pedals", svc.GetDeviceDescription("046d", "c52b"));
        Assert.Equal("My SimRacing Pedals", svc.GetDeviceDescription("0x046D", "C52B"));
    }

    [Theory]
    [InlineData("es", "mode_zerocopy", "Zero-Copy")]
    [InlineData("en", "mode_lowlatency", "Low-Latency")]
    [InlineData("es", "driver_installed", "Instalado")]
    [InlineData("en", "driver_installed", "Installed")]
    [InlineData("en", "menu_properties", "Properties")]
    [InlineData("es", "menu_properties", "Propiedades")]
    public void LocalizationService_ContainsAllNewKeys(string lang, string key, string expectedPart)
    {
        var loc = new LocalizationService();
        var val = loc.GetText(key, lang);
        Assert.NotEqual(key, val);
        Assert.Contains(expectedPart, val);
    }

    [Fact]
    public void UsbDevice_ExtractsVendorAndProductIdsSafely()
    {
        var dev = new UsbDevice { HardwareId = "046d:c52b" };
        Assert.Equal("046d", dev.VendorId);
        Assert.Equal("c52b", dev.ProductId);

        var empty = new UsbDevice { HardwareId = "" };
        Assert.Equal(string.Empty, empty.VendorId);
        Assert.Equal(string.Empty, empty.ProductId);
    }

    [Fact]
    public void ConnectionMonitorService_ShouldAttemptReconnect_UsesProgressiveBackoff()
    {
        var now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        var dev = new MonitoredDevice
        {
            ServerIP = "192.168.1.50",
            BusId = "1-1.3",
            AutoReconnect = true,
            IsConnected = false,
            ReconnectAttempts = 0,
            LastReconnectAttemptUtc = DateTime.MinValue
        };

        Assert.True(ConnectionMonitorService.ShouldAttemptReconnect(dev, now));

        // After 3 attempts (e.g. device unplugged > 6s), monitoring continues with progressive backoff
        dev.ReconnectAttempts = 3;
        dev.LastReconnectAttemptUtc = now.AddSeconds(-2);
        Assert.False(ConnectionMonitorService.ShouldAttemptReconnect(dev, now));
        Assert.True(ConnectionMonitorService.ShouldAttemptReconnect(dev, now.AddSeconds(3)));

        // Prevent re-entrancy while reconnect is in flight
        dev.IsReconnecting = true;
        Assert.False(ConnectionMonitorService.ShouldAttemptReconnect(dev, now.AddSeconds(30)));
    }

    [Fact]
    public void LoggerService_Initialize_UsesAssemblyVersionNotLegacyHardcoded()
    {
        var tempLogs = Path.Combine(Path.GetTempPath(), $"usbanywhere_logtest_{Guid.NewGuid():N}");
        try
        {
            var env = new StubEnvironmentService(tempLogs);
            var logger = new LoggerService(env);
            logger.Initialize();

            var logFiles = Directory.GetFiles(tempLogs, "usbanywhere_*.log");
            Assert.Single(logFiles);
            var content = File.ReadAllText(logFiles[0]);
            var expectedVer = typeof(LoggerService).Assembly.GetName().Version?.ToString(3) ?? "3.3.1";
            Assert.Contains($"v{expectedVer} Logger Initialized", content);
        }
        finally
        {
            if (Directory.Exists(tempLogs))
                Directory.Delete(tempLogs, recursive: true);
        }
    }

    [Fact]
    public void FavoritesService_AddFavorite_ForcesLowLatencyForDualSense()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"usbanywhere_favtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var env = new StubEnvironmentService(tempDir);
            var logger = new StubLoggerService();
            var cfg = new ConfigService(env, logger);
            var favorites = new FavoritesService(cfg, new StubUsbipService());

            var res = favorites.AddFavorite(
                "192.168.1.50",
                "1-1",
                "Sony Interactive Entertainment DualSense Edge Wireless Controller",
                autoConnect: true,
                receiveMode: "zero-copy");

            Assert.True(res.Success);
            var saved = favorites.GetFavorites().Single();
            Assert.Equal("low-latency", saved.ReceiveMode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void UpdateService_SelectClientAsset_ExcludesServerInstaller()
    {
        var assets = new List<UpdateService.GitHubAsset>
        {
            new() { Name = "USBAnywhere-Server_Setup_v3.3.1.exe", BrowserDownloadUrl = "https://example.com/server.exe" },
            new() { Name = "USBAnywhere_Setup_v3.3.1.exe", BrowserDownloadUrl = "https://example.com/client.exe" },
            new() { Name = "USBAnywhere-Server-Portable-x64.zip", BrowserDownloadUrl = "https://example.com/server.zip" },
            new() { Name = "USBAnywhere-Portable-x64.zip", BrowserDownloadUrl = "https://example.com/client.zip" }
        };

        var installedAsset = UpdateService.SelectClientAsset(assets, isInstalled: true);
        Assert.NotNull(installedAsset);
        Assert.Equal("USBAnywhere_Setup_v3.3.1.exe", installedAsset.Name);

        var portableAsset = UpdateService.SelectClientAsset(assets, isInstalled: false);
        Assert.NotNull(portableAsset);
        Assert.Equal("USBAnywhere-Portable-x64.zip", portableAsset.Name);
    }

    [Fact]
    public void ConnectionMonitorService_OfflineFavorite_StartsMonitoringWithInitiallyConnectedFalse()
    {
        using var monitor = new ConnectionMonitorService(new StubUsbipService(), new StubFavoritesService());

        monitor.AddDevice("192.168.1.50", "1-1", autoReconnect: true, receiveMode: "low-latency", initiallyConnected: false);
        var monitored = monitor.GetMonitoredDevice("192.168.1.50", "1-1");

        Assert.NotNull(monitored);
        Assert.False(monitored.IsConnected);
        Assert.True(monitored.AutoReconnect);
        Assert.Equal("low-latency", monitored.ReceiveMode);
        Assert.True(ConnectionMonitorService.ShouldAttemptReconnect(monitored, DateTime.UtcNow));
    }

    [Fact]
    public void App_HeadlessCliAndHotkeyParsing_WorksCleanly()
    {
        Assert.True(App.IsHeadlessCliInvocation(new[] { "--disconnect-all" }));
        Assert.True(App.IsHeadlessCliInvocation(new[] { "--disconnect", "0" }));
        Assert.True(App.IsHeadlessCliInvocation(new[] { "--quit" }));
        Assert.False(App.IsHeadlessCliInvocation(new[] { "--minimized" }));

        Assert.True(MainWindow.TryParseHotkey("Ctrl+Alt+D", out uint mods, out uint vk));
        Assert.Equal(0x0002u | 0x0001u, mods);
        Assert.Equal(0x44u, vk);
    }

    private sealed class StubEnvironmentService : IEnvironmentService
    {
        public StubEnvironmentService(string logsPath) => LogsPath = logsPath;
        public string AppDataPath => LogsPath;
        public string LogsPath { get; }
        public string BinPath => LogsPath;
        public string DriversPath => LogsPath;
        public string TempPath => LogsPath;
        public bool IsPortableMode => true;
        public bool IsPortable => true;
        public string GetConfigFilePath() => Path.Combine(LogsPath, "config.json");
        public void Initialize() { }
    }

    private sealed class StubLoggerService : ILoggerService
    {
        public void Initialize() { }
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? ex = null) { }
    }

    private sealed class StubUsbipService : IUsbipService
    {
        public Task<List<UsbDevice>> ListRemoteDevicesAsync(string ip, CancellationToken ct = default) => Task.FromResult(new List<UsbDevice>());
        public Task<bool> AttachDeviceAsync(string ip, string busId, string? receiveMode = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> DetachDeviceAsync(string port, CancellationToken ct = default) => Task.FromResult(true);
        public Task<int> DetachAllConnectedDevicesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<UsbDevice>> ListDevicesAsync(string serverIP) => Task.FromResult(new List<UsbDevice>());
        public Task<OperationResult> ConnectAsync(string serverIP, string busId, string? receiveMode = null) => Task.FromResult(OperationResult.Fail("offline"));
        public Task<OperationResult> DisconnectAsync(int port) => Task.FromResult(OperationResult.Ok());
        public Task<List<ConnectedDevice>> GetConnectedDevicesAsync() => Task.FromResult(new List<ConnectedDevice>());
    }

    private sealed class StubFavoritesService : IFavoritesService
    {
        public List<FavoriteDevice> GetFavorites() => new();
        public OperationResult AddFavorite(string serverIP, string busId, string description, bool autoConnect = true, string receiveMode = "zero-copy") => OperationResult.Ok();
        public OperationResult RemoveFavorite(string serverIP, string busId) => OperationResult.Ok();
        public bool IsFavorite(string serverIP, string busId) => false;
        public Task<List<FavoriteConnectResult>> ConnectFavoritesAsync() => Task.FromResult(new List<FavoriteConnectResult>());
    }
}
