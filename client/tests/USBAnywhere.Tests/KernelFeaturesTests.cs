using System.IO;
using System.Text;
using USBAnywhere.Services;
using Xunit;

namespace USBAnywhere.Tests;

public class KernelFeaturesTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("CAM001", true)]
    [InlineData("123456789012345", true)]
    [InlineData("1234567890123456", false)]
    [InlineData("CAM-001", false)]
    [InlineData("CAM 001", false)]
    public void IsValidUsbSerial_ValidatesAsciiAlphanumericAndLength(string? serial, bool expected)
    {
        Assert.Equal(expected, VhciNativeClient.IsValidUsbSerial(serial));
    }

    [Fact]
    public void GenerateUsbSerial_ProducesValid12CharAlphanumericSerial()
    {
        string s1 = VhciNativeClient.GenerateUsbSerial();
        string s2 = VhciNativeClient.GenerateUsbSerial();
        Assert.Equal(12, s1.Length);
        Assert.True(VhciNativeClient.IsValidUsbSerial(s1));
        Assert.NotEqual(s1, s2);
    }

    [Fact]
    public void BuildPluginPayload_WritesSerialAtExpectedAbiOffsets()
    {
        byte[] p981 = VhciNativeClient.BuildPluginPayload("192.168.1.50", "2-1", wskEvents: true, serial: "SER981", abi: VhciAbiVersion.V0981);
        Assert.Equal("SER981", Encoding.ASCII.GetString(p981, 0x450, 6));
        Assert.Equal(1, p981[0x460]);

        byte[] p982 = VhciNativeClient.BuildPluginPayload("192.168.1.50", "2-1", wskEvents: false, serial: "SER982", abi: VhciAbiVersion.V0982);
        Assert.Equal("SER982", Encoding.ASCII.GetString(p982, 0x44D, 6));
        Assert.Equal(0, p982[0x45D]);
    }

    [Theory]
    [InlineData(VhciAbiVersion.V0981)]
    [InlineData(VhciAbiVersion.V0982)]
    public void PersistentMultiSz_RoundTripsAcrossBothAbiFormats(VhciAbiVersion abi)
    {
        var input = new[]
        {
            new VhciPersistentDevice("192.168.1.50", "3240", "2-1", "CAM001", "low-latency", AttachOnce: false),
            new VhciPersistentDevice("10.0.0.5", "3240", "1-3", "", "zero-copy", AttachOnce: false)
        };
        byte[] raw = VhciNativeClient.BuildPersistentMultiSz(input, abi);
        var parsed = VhciNativeClient.ParsePersistentMultiSz(raw);
        Assert.Equal(2, parsed.Count);
        Assert.Equal("192.168.1.50", parsed[0].Host);
        Assert.Equal("2-1", parsed[0].BusId);
        Assert.Equal("CAM001", parsed[0].Serial);
        Assert.Equal("low-latency", parsed[0].ReceiveMode);
        Assert.Equal("zero-copy", parsed[1].ReceiveMode);
    }

    [Fact]
    public void PersistentMultiSz_EmptyListProducesDoubleNullTerminator()
    {
        byte[] raw = VhciNativeClient.BuildPersistentMultiSz(Array.Empty<VhciPersistentDevice>(), VhciAbiVersion.V0981);
        Assert.Equal(4, raw.Length);
        Assert.Empty(VhciNativeClient.ParsePersistentMultiSz(raw));
    }

    [Fact]
    public void BuildPlugoutPayload_EncodesPortAllCloseOnly()
    {
        byte[] raw = VhciNativeClient.BuildPlugoutPayload(VhciNativeClient.PortAllCloseOnly);
        Assert.Equal(8, raw.Length);
        Assert.Equal(-2, BitConverter.ToInt32(raw, 4));
    }

    [Theory]
    [InlineData(30, 30, true)]
    [InlineData(127, 128, true)]
    [InlineData(0, 30, false)]
    [InlineData(30, 0, false)]
    [InlineData(128, 128, false)]
    public void KernelConfigService_ValidatePortCounts_EnforcesBounds(int u20, int u30, bool expected)
    {
        Assert.Equal(expected, KernelConfigService.ValidatePortCounts(u20, u30, out _));
    }

    [Fact]
    public void KernelDiagnosticsService_ExportDiagnosticBundle_CreatesValidZip()
    {
        string outZip = Path.Combine(Path.GetTempPath(), $"USBAnywhere_TestDiag_{Guid.NewGuid():N}.zip");
        try
        {
            string created = KernelDiagnosticsService.ExportDiagnosticBundle(outZip, new[] { "line1", "line2" }, "Installed");
            Assert.True(File.Exists(created));
            Assert.True(new FileInfo(created).Length > 100);
        }
        finally
        {
            if (File.Exists(outZip)) File.Delete(outZip);
        }
    }
}
