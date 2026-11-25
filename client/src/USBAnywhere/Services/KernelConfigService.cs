// USBAnywhere - Virtual USB Hub Registry Configuration Service
using Microsoft.Win32;

using USBAnywhere.Models;

namespace USBAnywhere.Services;

public sealed record VirtualHubPortSettings(int Usb20Ports, int Usb30Ports);

public static class KernelConfigService
{
    public const string ParametersRegistryPath = @"SYSTEM\CurrentControlSet\Services\usbip2_ude\Parameters";
    public const int DefaultUsb20Ports = 30;
    public const int DefaultUsb30Ports = 30;
    public const int MaxTotalPorts = 255;

    public static bool ValidatePortCounts(int usb20Ports, int usb30Ports, out string? error)
    {
        if (usb20Ports < 1 || usb30Ports < 1)
        {
            error = "Both USB 2.0 and USB 3.0 port counts must be at least 1.";
            return false;
        }
        if (usb20Ports + usb30Ports > MaxTotalPorts)
        {
            error = $"Total virtual hub ports ({usb20Ports + usb30Ports}) cannot exceed {MaxTotalPorts}.";
            return false;
        }
        error = null;
        return true;
    }

    public static VirtualHubPortSettings GetVirtualHubPortSettings()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ParametersRegistryPath, writable: false);
            if (key == null)
                return new VirtualHubPortSettings(DefaultUsb20Ports, DefaultUsb30Ports);

            int u20 = key.GetValue("NumberOfUsb20Ports") is int v20 ? v20 : DefaultUsb20Ports;
            int u30 = key.GetValue("NumberOfUsb30Ports") is int v30 ? v30 : DefaultUsb30Ports;
            return ValidatePortCounts(u20, u30, out _)
                ? new VirtualHubPortSettings(u20, u30)
                : new VirtualHubPortSettings(DefaultUsb20Ports, DefaultUsb30Ports);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KernelConfig] Registry read failed: {ex.Message}");
            return new VirtualHubPortSettings(DefaultUsb20Ports, DefaultUsb30Ports);
        }
    }

    public static OperationResult SetVirtualHubPortSettings(int usb20Ports, int usb30Ports)
    {
        if (!ValidatePortCounts(usb20Ports, usb30Ports, out string? error))
            return OperationResult.Fail(error!);

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ParametersRegistryPath, writable: true)
                            ?? Registry.LocalMachine.CreateSubKey(ParametersRegistryPath, writable: true);
            if (key == null)
                return OperationResult.Fail("Unable to open driver Parameters registry key.");

            key.SetValue("NumberOfUsb20Ports", usb20Ports, RegistryValueKind.DWord);
            key.SetValue("NumberOfUsb30Ports", usb30Ports, RegistryValueKind.DWord);
            return OperationResult.Ok("Virtual hub port counts saved. Reboot or restart the VHCI device to apply.");
        }
        catch (UnauthorizedAccessException)
        {
            return OperationResult.Fail("Administrator privileges required to modify driver hub port settings.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}
