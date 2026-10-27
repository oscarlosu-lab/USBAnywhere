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

    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("DEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEF")]
    public void ValidHashes_Rejects_Untrusted_Hashes(string untrustedHash)
    {
        Assert.DoesNotContain(untrustedHash, UsbipService.ValidHashes);
        Assert.DoesNotContain(untrustedHash, UsbipService.ValidDllHashes);
    }
}
