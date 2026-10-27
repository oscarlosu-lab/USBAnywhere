using System.Collections.Generic;
using USBAnywhere.Models;
using USBAnywhere.Services;
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
        svc.SetCustomName("046d", "c52b", "My SimRacing Pedals");

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
}
