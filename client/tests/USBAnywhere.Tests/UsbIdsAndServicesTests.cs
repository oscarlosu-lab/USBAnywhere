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
            var expectedVer = typeof(LoggerService).Assembly.GetName().Version?.ToString(3) ?? "3.4.0";
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
            new() { Name = "USBAnywhere-Server_Setup_v3.4.0.exe", BrowserDownloadUrl = "https://example.com/server.exe" },
            new() { Name = "USBAnywhere_Setup_v3.4.0.exe", BrowserDownloadUrl = "https://example.com/client.exe" },
            new() { Name = "USBAnywhere-Server-Portable-x64.zip", BrowserDownloadUrl = "https://example.com/server.zip" },
            new() { Name = "USBAnywhere-Portable-x64.zip", BrowserDownloadUrl = "https://example.com/client.zip" }
        };

        var installedAsset = UpdateService.SelectClientAsset(assets, isInstalled: true);
        Assert.NotNull(installedAsset);
        Assert.Equal("USBAnywhere_Setup_v3.4.0.exe", installedAsset.Name);

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

        Assert.True(App.TryParseConnectArgs(new[] { "--minimized", "--connect", "192.168.11.115", "7-1" }, out string ip, out string busId));
        Assert.Equal("192.168.11.115", ip);
        Assert.Equal("7-1", busId);
        Assert.True(App.TryParseConnectArgs(new[] { "--attach", "10.0.0.5", "1-2.3" }, out ip, out busId));
        Assert.Equal("10.0.0.5", ip);
        Assert.Equal("1-2.3", busId);
        Assert.False(App.TryParseConnectArgs(new[] { "--connect", "bad host!", "7-1" }, out _, out _));
    }

    [Fact]
    public void VhciNativeClient_BuildPayloads_MatchV0981AndV0982Abis()
    {
        var plugout = VhciNativeClient.BuildPlugoutPayload(3);
        Assert.Equal(8, plugout.Length);
        Assert.Equal(8u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(plugout.AsSpan(0, 4)));
        Assert.Equal(3, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(plugout.AsSpan(4, 4)));

        var p981 = VhciNativeClient.BuildPluginPayload("192.168.1.50", "1-1.3", wskEvents: true, abi: VhciAbiVersion.V0981);
        Assert.Equal(1124, p981.Length);
        Assert.Equal(1124u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(p981.AsSpan(0, 4)));
        Assert.Equal((byte)1, p981[0x460]);
        Assert.StartsWith("1-1.3\0", System.Text.Encoding.ASCII.GetString(p981, 0x00C, 32));
        Assert.StartsWith("3240\0", System.Text.Encoding.ASCII.GetString(p981, 0x02C, 32));
        Assert.StartsWith("192.168.1.50\0", System.Text.Encoding.ASCII.GetString(p981, 0x04C, 32));

        var p982 = VhciNativeClient.BuildPluginPayload("10.0.0.5", "2-1", wskEvents: false, serial: "ABC123", abi: VhciAbiVersion.V0982);
        Assert.Equal(1120, p982.Length);
        Assert.Equal(1120u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(p982.AsSpan(0, 4)));
        Assert.StartsWith("ABC123\0", System.Text.Encoding.ASCII.GetString(p982, 0x44D, 16));
        Assert.Equal((byte)0, p982[0x45D]);

        Assert.Equal(1108, VhciNativeClient.BuildStopAttachPayload(abi: VhciAbiVersion.V0981).Length);
        Assert.Equal(1100, VhciNativeClient.BuildStopAttachPayload(abi: VhciAbiVersion.V0982).Length);
    }

    [Theory]
    [InlineData(VhciAbiVersion.V0981)]
    [InlineData(VhciAbiVersion.V0982)]
    public void VhciNativeClient_ParseImportedDevicesAndDeviceState_SupportsBothAbis(VhciAbiVersion abi)
    {
        var usbIds = new UsbIdsService();
        int total = 4 + VhciNativeClient.ImportedDeviceRecordSize;
        var buf = new byte[total];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0, 4), (uint)VhciNativeClient.GetImportedDevicesInitialSelfSize);

        var rec = buf.AsSpan(4, VhciNativeClient.ImportedDeviceRecordSize);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(rec.Slice(0, 4), 2);
        System.Text.Encoding.ASCII.GetBytes("1-1.3\0").CopyTo(rec.Slice(0x008));
        System.Text.Encoding.ASCII.GetBytes("3240\0").CopyTo(rec.Slice(0x028));
        System.Text.Encoding.ASCII.GetBytes("192.168.1.99\0").CopyTo(rec.Slice(0x048));

        if (abi == VhciAbiVersion.V0981)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(rec.Slice(0x44C, 4), 0x00010002u);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(rec.Slice(0x450, 4), 3);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(rec.Slice(0x454, 2), 0x046D);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(rec.Slice(0x456, 2), 0xC52B);
            rec[0x469] = 1;
        }
        else
        {
            System.Text.Encoding.ASCII.GetBytes("SER01\0").CopyTo(rec.Slice(0x449));
            rec[0x459] = 0;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(rec.Slice(0x45C, 4), 0x00010002u);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(rec.Slice(0x460, 4), 3);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(rec.Slice(0x464, 2), 0x046D);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(rec.Slice(0x466, 2), 0xC52B);
        }

        var devices = VhciNativeClient.ParseImportedDevicesBuffer(buf, total, usbIds);
        Assert.NotNull(devices);
        var dev = Assert.Single(devices);
        Assert.Equal(2, dev.Port);
        Assert.Equal("1-1.3", dev.BusId);
        Assert.Equal("192.168.1.99", dev.ServerIP);
        Assert.Equal("046d", dev.VendorId);
        Assert.Equal("c52b", dev.ProductId);
        Assert.Contains("Unifying Receiver", dev.Description);

        // Also validate 1144-byte push device_state packet parsing
        var pkt = new byte[VhciNativeClient.DeviceStatePacketSize];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(pkt.AsSpan(0, 4), VhciNativeClient.DeviceStatePacketSize);
        if (abi == VhciAbiVersion.V0981)
        {
            rec.CopyTo(pkt.AsSpan(4));
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(pkt.AsSpan(0x470, 4), (int)VhciDeviceState.Plugged);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(pkt.AsSpan(0x474, 4), 42u);
        }
        else
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(pkt.AsSpan(4, 4), (int)VhciDeviceState.Plugged);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(pkt.AsSpan(8, 4), 42u);
            rec.CopyTo(pkt.AsSpan(12));
        }

        var evt = VhciNativeClient.ParseDeviceStatePacket(pkt, usbIds);
        Assert.NotNull(evt);
        Assert.Equal(VhciDeviceState.Plugged, evt.State);
        Assert.Equal(42u, evt.SourceId);
        Assert.Equal(abi == VhciAbiVersion.V0981 ? "low-latency" : "zero-copy", evt.ReceiveMode);
        Assert.Equal("192.168.1.99", evt.Device.ServerIP);
        Assert.Equal("1-1.3", evt.Device.BusId);
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
