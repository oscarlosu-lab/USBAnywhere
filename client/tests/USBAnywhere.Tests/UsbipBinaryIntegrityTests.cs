using System;
using System.IO;
using System.Security.Cryptography;
using USBAnywhere.Services;
using Xunit;

namespace USBAnywhere.Tests;

public class UsbipBinaryIntegrityTests
{
    private static string CalculateSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var bytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(bytes).Replace("-", "").ToUpperInvariant();
    }

    private static string? FindFileUpwards(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public void ValidHashes_Contains_Bundled_x64_UsbipBinary()
    {
        var portablePath = FindFileUpwards(Path.Combine("Portable", "usbipw.exe"));
        Assert.NotNull(portablePath);
        var actualHash = CalculateSha256(portablePath);

        Assert.Contains(actualHash, UsbipService.ValidHashes);
    }

    [Fact]
    public void ValidHashes_Contains_Bundled_ARM64_UsbipBinary_IfPresent()
    {
        var arm64Path = FindFileUpwards(Path.Combine("Portable-ARM64", "usbipw.exe"));
        if (arm64Path != null)
        {
            var actualHash = CalculateSha256(arm64Path);
            Assert.Contains(actualHash, UsbipService.ValidHashes);
        }
    }

    [Fact]
    public void ValidDllHashes_Contains_Bundled_x64_LibusbipDll()
    {
        var dllPath = FindFileUpwards(Path.Combine("Portable", "libusbip.dll"));
        Assert.NotNull(dllPath);
        var actualHash = CalculateSha256(dllPath);

        Assert.Contains(actualHash, UsbipService.ValidDllHashes);
    }

    [Fact]
    public void ValidDllHashes_Contains_Bundled_ARM64_LibusbipDll_IfPresent()
    {
        var dllPath = FindFileUpwards(Path.Combine("Portable-ARM64", "libusbip.dll"));
        if (dllPath != null)
        {
            var actualHash = CalculateSha256(dllPath);
            Assert.Contains(actualHash, UsbipService.ValidDllHashes);
        }
    }

    [Fact]
    public void ValidDllHashes_Contains_Bundled_x64_ResourcesDll()
    {
        var dllPath = FindFileUpwards(Path.Combine("Portable", "resources.dll"));
        Assert.NotNull(dllPath);
        var actualHash = CalculateSha256(dllPath);

        Assert.Contains(actualHash, UsbipService.ValidDllHashes);
    }

    [Fact]
    public void ValidDllHashes_Contains_Bundled_ARM64_ResourcesDll_IfPresent()
    {
        var dllPath = FindFileUpwards(Path.Combine("Portable-ARM64", "resources.dll"));
        if (dllPath != null)
        {
            var actualHash = CalculateSha256(dllPath);
            Assert.Contains(actualHash, UsbipService.ValidDllHashes);
        }
    }

    [Fact]
    public void KnownDevnodeHashes_Contains_Bundled_x64_DevnodeExe()
    {
        var exePath = FindFileUpwards(Path.Combine("Portable", "devnode.exe"));
        Assert.NotNull(exePath);
        var actualHash = CalculateSha256(exePath);

        Assert.Contains(actualHash, DriverService.KnownDevnodeHashes);
    }

    [Fact]
    public void KnownDevnodeHashes_Contains_Bundled_ARM64_DevnodeExe_IfPresent()
    {
        var exePath = FindFileUpwards(Path.Combine("Portable-ARM64", "devnode.exe"));
        if (exePath != null)
        {
            var actualHash = CalculateSha256(exePath);
            Assert.Contains(actualHash, DriverService.KnownDevnodeHashes);
        }
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("DEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEF")]
    public void ValidHashes_Rejects_Untrusted_Hashes(string untrustedHash)
    {
        Assert.DoesNotContain(untrustedHash, UsbipService.ValidHashes);
        Assert.DoesNotContain(untrustedHash, UsbipService.ValidDllHashes);
        Assert.DoesNotContain(untrustedHash, DriverService.KnownDevnodeHashes);
    }

    [Fact]
    public void RemoveFilterFromMultiString_RemovesOnlyUsbip2Filter_PreservingSystemFilters()
    {
        var input = new[] { "TsUsbFlt", "usbip2_filter", "USBIP2_FILTER", "usbfilter" };
        var cleaned = DriverService.RemoveFilterFromMultiString(input);

        Assert.Equal(2, cleaned.Length);
        Assert.Contains("TsUsbFlt", cleaned);
        Assert.Contains("usbfilter", cleaned);
        Assert.DoesNotContain("usbip2_filter", cleaned, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemoveFilterFromMultiString_ReturnsEmpty_WhenOnlyUsbip2FilterPresent()
    {
        var input = new[] { "usbip2_filter" };
        var cleaned = DriverService.RemoveFilterFromMultiString(input);

        Assert.Empty(cleaned);
        Assert.Empty(DriverService.RemoveFilterFromMultiString(null));
    }

    [Theory]
    [InlineData("zero-copy", "Sony Interactive Entertainment DualSense Wireless Controller", "054c:0ce6", "low-latency")]
    [InlineData("zero-copy", "GameSir 2.4G Wireless Dongle", "3537:1040", "low-latency")]
    [InlineData("zero-copy", "Logitech USB Receiver", "046d:c53f", "low-latency")]
    [InlineData("zero-copy", "USB Audio Headset Microphone", "0d8c:0014", "low-latency")]
    [InlineData("zero-copy", "SanDisk Ultra Mass Storage", "0781:5581", "zero-copy")]
    [InlineData("low-latency", "SanDisk Ultra Mass Storage", "0781:5581", "low-latency")]
    public void ResolveEffectiveReceiveMode_SelectsLowLatencyForInteractiveDevices(
        string requested, string description, string hwId, string expected)
    {
        var actual = UsbipService.ResolveEffectiveReceiveMode(requested, description, hwId);
        Assert.Equal(expected, actual);
    }
}
