using System.Text.Json;
using USBAnywhere.Services;
using Xunit;

namespace USBAnywhere.Tests;

public class DynamicInventoryParserTests
{
    [Fact]
    public void ParsesAndroidObjectInventory()
    {
        using var document = JsonDocument.Parse(
            """{"busid":"1-2","vid":"046d","pid":"c52b","description":"USB Receiver","shared":true}""");

        var parsed = UsbipService.TryParseDynamicInventoryItem(
            document.RootElement,
            "192.0.2.10",
            out var device);

        Assert.True(parsed);
        Assert.NotNull(device);
        Assert.Equal("1-2", device.BusId);
        Assert.Equal("046d:c52b", device.HardwareId);
        Assert.Equal("USB Receiver", device.Description);
        Assert.Equal("192.0.2.10", device.RemoteIp);
    }

    [Fact]
    public void ContinuesParsingTextInventoryLines()
    {
        using var document = JsonDocument.Parse(
            """{"busid":"1-2 : USB Receiver (046d:c52b)"}""");

        var parsed = UsbipService.TryParseDynamicInventoryItem(
            document.RootElement.GetProperty("busid"),
            "192.0.2.10",
            out var device);

        Assert.True(parsed);
        Assert.NotNull(device);
        Assert.Equal("1-2", device.BusId);
        Assert.Equal("046d:c52b", device.HardwareId);
    }
}
