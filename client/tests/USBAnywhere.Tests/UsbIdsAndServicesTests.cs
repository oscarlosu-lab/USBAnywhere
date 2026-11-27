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
            var expectedVer = typeof(LoggerService).Assembly.GetName().Version?.ToString(3) ?? "3.4.1";
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
            new() { Name = "USBAnywhere-Server_Setup_v3.4.1.exe", BrowserDownloadUrl = "https://example.com/server.exe" },
            new() { Name = "USBAnywhere_Setup_v3.4.1.exe", BrowserDownloadUrl = "https://example.com/client.exe" },
            new() { Name = "USBAnywhere-Server-Portable-x64.zip", BrowserDownloadUrl = "https://example.com/server.zip" },
            new() { Name = "USBAnywhere-Portable-x64.zip", BrowserDownloadUrl = "https://example.com/client.zip" }
        };

        var installedAsset = UpdateService.SelectClientAsset(assets, isInstalled: true);
        Assert.NotNull(installedAsset);
        Assert.Equal("USBAnywhere_Setup_v3.4.1.exe", installedAsset.Name);

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
        public Task<bool> AttachDeviceAsync(string ip, string busId, string? receiveMode = null, CancellationToken ct = default, string? serial = null, bool attachOnce = true) => Task.FromResult(false);
        public Task<bool> DetachDeviceAsync(string port, CancellationToken ct = default) => Task.FromResult(true);
        public Task<int> DetachAllConnectedDevicesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<UsbDevice>> ListDevicesAsync(string serverIP) => Task.FromResult(new List<UsbDevice>());
        public Task<OperationResult> ConnectAsync(string serverIP, string busId, string? receiveMode = null, string? serial = null, bool attachOnce = true) => Task.FromResult(OperationResult.Fail("offline"));
        public Task<List<VhciPersistentDevice>> GetKernelPersistentDevicesAsync() => Task.FromResult(new List<VhciPersistentDevice>());
        public Task<OperationResult> SetKernelPersistentDevicesAsync(IReadOnlyList<VhciPersistentDevice> devices) => Task.FromResult(OperationResult.Ok());
        public Task<int> StopKernelAttachAttemptsAsync(string? host = null, string? busId = null) => Task.FromResult(0);
        public Task<OperationResult> DisconnectAsync(int port) => Task.FromResult(OperationResult.Ok());
        public Task<List<ConnectedDevice>> GetConnectedDevicesAsync() => Task.FromResult(new List<ConnectedDevice>());
        public List<ConnectedDevice> DetachAllConnectedDevicesFastForSuspend() => new();
        public Task NotifyRemoteUnbindBestEffortAsync(IEnumerable<(string ServerIP, string BusId)> bindings, int timeoutMs = 800) => Task.CompletedTask;
    }

    [Fact]
    public async Task FavoritesService_ResolveFavoriteTargetAsync_SurvivesDhcpAndPortMove()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"usbany_fav_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);
        try
        {
            var cfg = new ConfigService(new StubEnvironmentService(tmp), new StubLoggerService());
            var usbip = new SmartMockUsbipService();
            var favSvc = new FavoritesService(
                cfg,
                usbip,
                (host, _) => Task.FromResult(new[] { System.Net.IPAddress.Parse("192.168.1.99") }));

            favSvc.AddFavorite("192.168.1.50", "1-2", "Logitech Wheel", true, "low-latency", "046d:c266", serverHostname: "simrig.local");
            var fav = favSvc.GetFavorites().Single();

            var (ip, busId, changed) = await favSvc.ResolveFavoriteTargetAsync(fav);
            Assert.True(changed);
            Assert.Equal("192.168.1.99", ip);
            Assert.Equal("1-4", busId);

            var persisted = favSvc.GetFavorites().Single();
            Assert.Equal("192.168.1.99", persisted.ServerIP);
            Assert.Equal("1-4", persisted.BusId);
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
    }

    [Fact]
    public async Task FavoritesService_ResolveFavoriteTargetAsync_RefusesAmbiguousHardwareIdRemap()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"usbany_fav_amb_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);
        try
        {
            var cfg = new ConfigService(new StubEnvironmentService(tmp), new StubLoggerService());
            var usbip = new AmbiguousMockUsbipService();
            var favSvc = new FavoritesService(cfg, usbip);
            favSvc.AddFavorite("192.168.1.99", "1-9", "Generic Dongle", true, "low-latency", "1234:5678");
            var fav = favSvc.GetFavorites().Single();

            var (ip, busId, changed) = await favSvc.ResolveFavoriteTargetAsync(fav);
            Assert.False(changed);
            Assert.Equal("1-9", busId);
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
    }

    [Fact]
    public void App_HeadlessCli_RecognizesStatusAndDoctorAndBuildsValidJson()
    {
        Assert.True(App.IsHeadlessCliInvocation(new[] { "--status", "--json" }));
        Assert.True(App.IsHeadlessCliInvocation(new[] { "--doctor" }));
        Assert.False(App.IsHeadlessCliInvocation(new[] { "--minimized" }));

        var json = App.BuildHeadlessReportJson(
            isDoctor: true,
            version: "3.5.0",
            driver: new DriverStatus { Installed = true, Status = "OK" },
            connected: new List<ConnectedDevice> { new() { Port = 0, ServerIP = "192.168.1.10", BusId = "1-1" } },
            favoritesCount: 2,
            autoDetachOnSleep: true,
            isPortableOs: false);

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("doctor", doc.RootElement.GetProperty("mode").GetString());
        Assert.True(doc.RootElement.GetProperty("healthy").GetBoolean());
        Assert.Equal(1, doc.RootElement.GetProperty("connectedCount").GetInt32());
    }

    private sealed class SmartMockUsbipService : IUsbipService
    {
        public Task<List<UsbDevice>> ListRemoteDevicesAsync(string ip, CancellationToken ct = default)
        {
            if (ip == "192.168.1.99")
            {
                return Task.FromResult(new List<UsbDevice>
                {
                    new() { ServerIP = ip, BusId = "1-4", HardwareId = "046d:c266", Description = "Logitech Wheel" }
                });
            }
            return Task.FromResult(new List<UsbDevice>());
        }
        public Task<bool> AttachDeviceAsync(string ip, string busId, string? receiveMode = null, CancellationToken ct = default, string? serial = null, bool attachOnce = true) => Task.FromResult(true);
        public Task<bool> DetachDeviceAsync(string port, CancellationToken ct = default) => Task.FromResult(true);
        public Task<int> DetachAllConnectedDevicesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<UsbDevice>> ListDevicesAsync(string serverIP) => ListRemoteDevicesAsync(serverIP);
        public Task<OperationResult> ConnectAsync(string serverIP, string busId, string? receiveMode = null, string? serial = null, bool attachOnce = true) => Task.FromResult(OperationResult.Ok());
        public Task<List<VhciPersistentDevice>> GetKernelPersistentDevicesAsync() => Task.FromResult(new List<VhciPersistentDevice>());
        public Task<OperationResult> SetKernelPersistentDevicesAsync(IReadOnlyList<VhciPersistentDevice> devices) => Task.FromResult(OperationResult.Ok());
        public Task<int> StopKernelAttachAttemptsAsync(string? host = null, string? busId = null) => Task.FromResult(0);
        public Task<OperationResult> DisconnectAsync(int port) => Task.FromResult(OperationResult.Ok());
        public Task<List<ConnectedDevice>> GetConnectedDevicesAsync() => Task.FromResult(new List<ConnectedDevice>());
        public List<ConnectedDevice> DetachAllConnectedDevicesFastForSuspend() => new();
        public Task NotifyRemoteUnbindBestEffortAsync(IEnumerable<(string ServerIP, string BusId)> bindings, int timeoutMs = 800) => Task.CompletedTask;
    }

    private sealed class AmbiguousMockUsbipService : IUsbipService
    {
        public Task<List<UsbDevice>> ListRemoteDevicesAsync(string ip, CancellationToken ct = default) =>
            Task.FromResult(new List<UsbDevice>
            {
                new() { ServerIP = ip, BusId = "1-2", HardwareId = "1234:5678", Description = "Generic Dongle" },
                new() { ServerIP = ip, BusId = "1-3", HardwareId = "1234:5678", Description = "Generic Dongle" }
            });
        public Task<bool> AttachDeviceAsync(string ip, string busId, string? receiveMode = null, CancellationToken ct = default, string? serial = null, bool attachOnce = true) => Task.FromResult(true);
        public Task<bool> DetachDeviceAsync(string port, CancellationToken ct = default) => Task.FromResult(true);
        public Task<int> DetachAllConnectedDevicesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<UsbDevice>> ListDevicesAsync(string serverIP) => ListRemoteDevicesAsync(serverIP);
        public Task<OperationResult> ConnectAsync(string serverIP, string busId, string? receiveMode = null, string? serial = null, bool attachOnce = true) => Task.FromResult(OperationResult.Ok());
        public Task<List<VhciPersistentDevice>> GetKernelPersistentDevicesAsync() => Task.FromResult(new List<VhciPersistentDevice>());
        public Task<OperationResult> SetKernelPersistentDevicesAsync(IReadOnlyList<VhciPersistentDevice> devices) => Task.FromResult(OperationResult.Ok());
        public Task<int> StopKernelAttachAttemptsAsync(string? host = null, string? busId = null) => Task.FromResult(0);
        public Task<OperationResult> DisconnectAsync(int port) => Task.FromResult(OperationResult.Ok());
        public Task<List<ConnectedDevice>> GetConnectedDevicesAsync() => Task.FromResult(new List<ConnectedDevice>());
        public List<ConnectedDevice> DetachAllConnectedDevicesFastForSuspend() => new();
        public Task NotifyRemoteUnbindBestEffortAsync(IEnumerable<(string ServerIP, string BusId)> bindings, int timeoutMs = 800) => Task.CompletedTask;
    }

    private sealed class StubFavoritesService : IFavoritesService
    {
        public List<FavoriteDevice> GetFavorites() => new();
        public OperationResult AddFavorite(string serverIP, string busId, string description, bool autoConnect = true, string receiveMode = "zero-copy") => OperationResult.Ok();
        public OperationResult AddFavorite(string serverIP, string busId, string description, bool autoConnect, string receiveMode, string? hardwareId, string? serverHostname = null, string? deviceSerial = null, string? revision = null) => OperationResult.Ok();
        public OperationResult RemoveFavorite(string serverIP, string busId) => OperationResult.Ok();
        public bool IsFavorite(string serverIP, string busId) => false;
        public Task<(string ServerIP, string BusId, bool ResolvedChanged)> ResolveFavoriteTargetAsync(FavoriteDevice favorite, CancellationToken ct = default) => Task.FromResult((favorite.ServerIP, favorite.BusId, false));
        public Task<List<FavoriteConnectResult>> ConnectFavoritesAsync() => Task.FromResult(new List<FavoriteConnectResult>());
    }

    private sealed class InMemoryConfigStub : IConfigService
    {
        public AppConfig Current { get; set; } = new();
        public AppConfig Load() => Current;
        public void Save(AppConfig config) => Current = config;
        public void SetLanguage(string language) => Current.Language = language;
    }

    private sealed class StubDriverService : IDriverService
    {
        public string GetDriverStatus() => "Installed";
        public Task<bool> IsDriverInstalledAsync() => Task.FromResult(true);
        public Task<bool> InstallDriverAsync() => Task.FromResult(true);
        public Task<bool> UninstallDriverAsync() => Task.FromResult(true);
    }

    private sealed class DualDongleUsbipStub : IUsbipService
    {
        public List<UsbDevice> Devices { get; init; } = new();
        public Task<List<UsbDevice>> ListRemoteDevicesAsync(string serverIP, CancellationToken ct = default) => Task.FromResult(Devices);
        public Task<bool> AttachDeviceAsync(string ip, string busId, string? receiveMode = null, CancellationToken ct = default, string? serial = null, bool attachOnce = true) => Task.FromResult(true);
        public Task<List<UsbDevice>> ListDevicesAsync(string serverIP) => Task.FromResult(Devices);
        public Task<OperationResult> ConnectAsync(string serverIP, string busId, string? receiveMode = null, string? serial = null, bool attachOnce = true) => Task.FromResult(OperationResult.Ok());
        public Task<List<VhciPersistentDevice>> GetKernelPersistentDevicesAsync() => Task.FromResult(new List<VhciPersistentDevice>());
        public Task<OperationResult> SetKernelPersistentDevicesAsync(IReadOnlyList<VhciPersistentDevice> devices) => Task.FromResult(OperationResult.Ok());
        public Task<int> StopKernelAttachAttemptsAsync(string? host = null, string? busId = null) => Task.FromResult(0);
        public Task<OperationResult> DisconnectAsync(int port) => Task.FromResult(OperationResult.Ok());
        public Task<List<ConnectedDevice>> GetConnectedDevicesAsync() => Task.FromResult(new List<ConnectedDevice>());
        public Task<bool> DetachDeviceAsync(string port, CancellationToken ct = default) => Task.FromResult(true);
        public Task<int> DetachAllConnectedDevicesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public List<ConnectedDevice> DetachAllConnectedDevicesFastForSuspend() => new();
        public Task NotifyRemoteUnbindBestEffortAsync(IEnumerable<(string ServerIP, string BusId)> bindings, int timeoutMs = 800) => Task.CompletedTask;
    }

    [Fact]
    public async Task FavoritesService_DisambiguatesDualDonglesByRevision()
    {
        var usbip = new DualDongleUsbipStub
        {
            Devices = new List<UsbDevice>
            {
                new() { BusId = "1-2", HardwareId = "045e:02fe", Revision = "0100", Description = "Xbox Wireless Adapter" },
                new() { BusId = "1-5", HardwareId = "045e:02fe", Revision = "0200", Description = "Xbox Wireless Adapter" }
            }
        };
        var cfg = new InMemoryConfigStub();
        cfg.Current.Favorites.Add(new FavoriteDevice
        {
            ServerIP = "203.0.113.10",
            BusId = "1-1",
            HardwareId = "045e:02fe",
            Revision = "0200",
            Description = "Xbox Wireless Adapter"
        });
        var favSvc = new FavoritesService(cfg, usbip);
        var resolved = await favSvc.ResolveFavoriteTargetAsync(cfg.Current.Favorites[0]);
        Assert.Equal("1-5", resolved.BusId);
        Assert.True(resolved.ResolvedChanged);
    }

    [Fact]
    public void PairingClientService_ParseUa2Token_DecodesValidToken()
    {
        var raw = "203.0.113.10|3240|LabHost|3243|654321|0123456789abcdef";
        var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var parsed = PairingClientService.ParseUa2Token("UA2:" + b64);
        Assert.NotNull(parsed);
        Assert.Equal("203.0.113.10", parsed!.Value.Host);
        Assert.Equal(3240, parsed.Value.UsbipPort);
        Assert.Equal("LabHost", parsed.Value.ServerName);
        Assert.Equal(3243, parsed.Value.TlsPort);
        Assert.Equal("654321", parsed.Value.Code);
        Assert.Equal("0123456789abcdef", parsed.Value.TlsFingerprint);
    }

    [Fact]
    public async Task TlsTunnelBridge_LoopbackValidPinSucceeds_AndWrongPinFails()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=USBAnywhereTest", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var rawCert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        using var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(
            rawCert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx), null);
        var fp = Convert.ToHexString(cert.GetCertHash(System.Security.Cryptography.HashAlgorithmName.SHA256)).ToLowerInvariant();

        Assert.True(TlsTunnelBridge.VerifyCertificateFingerprint(cert.GetCertHash(System.Security.Cryptography.HashAlgorithmName.SHA256), fp));
        Assert.False(TlsTunnelBridge.VerifyCertificateFingerprint(cert.GetCertHash(System.Security.Cryptography.HashAlgorithmName.SHA256), new string('0', 64)));

        using var tlsListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        tlsListener.Start();
        int tlsPort = ((System.Net.IPEndPoint)tlsListener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var c = await tlsListener.AcceptTcpClientAsync();
            using var ssl = new System.Net.Security.SslStream(c.GetStream(), false);
            await ssl.AuthenticateAsServerAsync(cert, false, false);
            var buf = new byte[4];
            int n = await ssl.ReadAsync(buf);
            await ssl.WriteAsync(buf.AsMemory(0, n));
        });

        using var bridge = new TlsTunnelBridge("127.0.0.1", tlsPort, fp);
        int localPort = bridge.Start();
        using var tcp = new System.Net.Sockets.TcpClient();
        await tcp.ConnectAsync("127.0.0.1", localPort);
        using var ns = tcp.GetStream();
        await ns.WriteAsync(new byte[] { 0x50, 0x49, 0x4E, 0x47 });
        var resp = new byte[4];
        int read = await ns.ReadAsync(resp);
        Assert.Equal(4, read);
        Assert.Equal("PING", System.Text.Encoding.ASCII.GetString(resp));
        await serverTask;
    }

    [Fact]
    public void StreamingSessionMonitor_DetectsProviders_AndFiltersOnlyDuringStream()
    {
        Assert.Equal("apollo", StreamingSessionMonitor.DetectStreamingProvider(new[] { "explorer", "apollo.exe" }));
        Assert.Equal("vibepollo", StreamingSessionMonitor.DetectStreamingProvider(new[] { "vibepollo" }));
        Assert.Equal("vibeshine", StreamingSessionMonitor.DetectStreamingProvider(new[] { "vibeshine.exe" }));
        Assert.Equal("parsec", StreamingSessionMonitor.DetectStreamingProvider(new[] { "parsecd" }));
        Assert.Equal("steam_remote_play", StreamingSessionMonitor.DetectStreamingProvider(new[] { "streaming_client.exe" }));
        Assert.Equal("sunshine", StreamingSessionMonitor.DetectStreamingProvider(new[] { "sunshine" }));
        Assert.Null(StreamingSessionMonitor.DetectStreamingProvider(new[] { "notepad" }));

        var favs = new List<FavoriteDevice>
        {
            new() { BusId = "1-1", AttachMode = "always" },
            new() { BusId = "1-2", AttachMode = "only-during-stream" }
        };
        var filtered = StreamingSessionMonitor.FilterFavoritesForStreamTransition(favs, true);
        Assert.Single(filtered);
        Assert.Equal("1-2", filtered[0].BusId);
    }

    [Fact]
    public async Task LocalControlApiService_ServesStatusDoctorStream_AndEnforcesBearerToken()
    {
        var cfg = new InMemoryConfigStub();
        cfg.Current.LocalApiEnabled = true;
        cfg.Current.LocalApiToken = "test-secret-token";
        var monitor = new StreamingSessionMonitor();
        using var api = new LocalControlApiService(cfg, new DualDongleUsbipStub(), new StubDriverService(), new StubFavoritesService(), monitor, 0);
        api.Start();

        using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{api.Port}") };

        var unauth = await http.GetAsync("/api/v1/status");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, unauth.StatusCode);

        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-secret-token");
        var status = await http.GetStringAsync("/api/v1/status");
        Assert.Contains("\"connectedCount\"", status);

        var doctor = await http.GetStringAsync("/api/v1/doctor");
        Assert.Contains("\"healthy\":true", doctor);

        var streamBody = new System.Net.Http.StringContent("{\"active\":true,\"provider\":\"apollo\"}", System.Text.Encoding.UTF8, "application/json");
        var streamResp = await http.PostAsync("/api/v1/stream", streamBody);
        Assert.Equal(System.Net.HttpStatusCode.OK, streamResp.StatusCode);
        Assert.True(monitor.IsStreamActive);
        Assert.Equal("apollo", monitor.ActiveProvider);
    }

    [Fact]
    public void SessionIsolationService_EnforcesPortOwnershipAndAutoConnect()
    {
        var iso = new SessionIsolationService();
        iso.RegisterPortOwner(2, sessionId: 1);
        Assert.True(iso.CanSessionControlPort(2, sessionId: 1, enforceIsolation: true));
        Assert.False(iso.CanSessionControlPort(2, sessionId: 2, enforceIsolation: true));
        Assert.True(iso.CanSessionControlPort(2, sessionId: 2, enforceIsolation: false));
        iso.ReleasePortOwner(2);
        Assert.Null(iso.GetPortOwner(2));

        Assert.False(SessionIsolationService.EvaluateAutoConnect(isCurrentSessionActive: false, enforceIsolation: true));
        Assert.True(SessionIsolationService.EvaluateAutoConnect(isCurrentSessionActive: true, enforceIsolation: true));
        Assert.True(SessionIsolationService.EvaluateAutoConnect(isCurrentSessionActive: false, enforceIsolation: false));
    }

    [Fact]
    public void UsbipService_ResolveAttachEndpoint_RoutesPairedTlsThroughLocalBridge()
    {
        var cfg = new InMemoryConfigStub();
        cfg.Current.PairedServers.Add(new PairedServerEntry
        {
            Host = "203.0.113.10",
            TlsPort = 3243,
            TlsFingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            UseTls = true
        });
        var svc = new UsbipService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UsbipService>.Instance,
            new EnvironmentService(),
            new DriverService(new EnvironmentService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DriverService>.Instance),
            new UsbIdsService(),
            cfg);

        var (hostTls, portTls, usingTls) = svc.ResolveAttachEndpoint("203.0.113.10");
        Assert.True(usingTls);
        Assert.Equal("127.0.0.1", hostTls);
        Assert.True(int.Parse(portTls) > 0);

        var (hostPlain, portPlain, usingPlainTls) = svc.ResolveAttachEndpoint("203.0.113.20");
        Assert.False(usingPlainTls);
        Assert.Equal("203.0.113.20", hostPlain);
        Assert.Equal("3240", portPlain);
    }
}
