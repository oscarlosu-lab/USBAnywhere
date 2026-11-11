using System;
using System.Collections.Generic;
using System.IO;
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

        dev.ReconnectAttempts = 3;
        dev.LastReconnectAttemptUtc = now.AddSeconds(-2);
        Assert.False(ConnectionMonitorService.ShouldAttemptReconnect(dev, now));
        Assert.True(ConnectionMonitorService.ShouldAttemptReconnect(dev, now.AddSeconds(3)));

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
}
