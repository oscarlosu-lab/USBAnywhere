// ═══════════════════════════════════════════════════════════════════════════════
// USBAnywhere - Direct Native VHCI IOCTL Client for Windows (UDE VHCI driver)
// Eliminates usbip.exe process spawning, stdout regex parsing, and SHA-256 overhead.
// ═══════════════════════════════════════════════════════════════════════════════

using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using USBAnywhere.Models;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

public enum VhciAbiVersion
{
    V0981 = 1,
    V0982 = 2
}

public enum VhciDeviceState
{
    Unplugged = 0,
    Connecting = 1,
    Connected = 2,
    Plugged = 3,
    Disconnected = 4,
    Unplugging = 5
}

public sealed record VhciDeviceStateEvent(
    VhciDeviceState State,
    uint SourceId,
    ConnectedDevice Device,
    string ReceiveMode);

/// <summary>
/// Direct P/Invoke Win32 IOCTL client for the usbip2_ude kernel VHCI driver
/// (<c>GUID_DEVINTERFACE_USBIP_VHCI</c>), supporting multiple VHCI driver revisions
/// as well as overlapped push event notifications via <c>ReadFile</c>.
/// </summary>
public static class VhciNativeClient
{
    // DEFINE_GUID(GUID_DEVINTERFACE_USBIP_VHCI, 0xB4030C06, 0xDC5F, 0x4FCC, 0x87, 0xEB, 0xE5, 0x51, 0x5A, 0x09, 0x35, 0xC0)
    public static readonly Guid GuidDevInterfaceUsbipVhci = new("B4030C06-DC5F-4FCC-87EB-E5515A0935C0");

    public const uint IoctlPluginHardware = 0x0022E000u;
    public const uint IoctlPlugoutHardware = 0x0022E004u;
    public const uint IoctlGetImportedDevices = 0x0022E008u;
    public const uint IoctlStopAttachAttempts = 0x0022E014u;
    public const uint IoctlPluginHardwareOnce = 0x0022E018u;

    public const int PortAll = -1;
    public const string DefaultServicePort = "3240";

    public const int BusIdSize = 32;
    public const int ServiceSize = 32;
    public const int HostSize = 1025;
    public const int SerialBufSize = 16;

    public const int ImportedDeviceHeaderSize = 4;
    public const int ImportedDeviceRecordSize = 1132;
    public const int GetImportedDevicesInitialSelfSize = 1136;
    public const int DeviceStatePacketSize = 1144;

    public const int PluginHardwareSizeV0981 = 1124;
    public const int PluginHardwareSizeV0982 = 1120;
    public const int StopAttachAttemptsSizeV0981 = 1108;
    public const int StopAttachAttemptsSizeV0982 = 1100;
    public const int PlugoutHardwareSize = 8;

    private const uint CrSuccess = 0x00000000u;
    private const uint CrBufferSmall = 0x0000001Au;
    private const uint CmGetDeviceInterfaceListPresent = 0x00000000u;

    private const uint GenericRead = 0x80000000u;
    private const uint GenericWrite = 0x40000000u;
    private const uint FileShareRead = 0x00000001u;
    private const uint FileShareWrite = 0x00000002u;
    private const uint OpenExisting = 3u;
    private const uint FileAttributeNormal = 0x00000080u;
    private const uint FileFlagOverlapped = 0x40000000u;

    private const int ErrorAccessDenied = 5;
    private const int ErrorInvalidParameter = 87;
    private const int ErrorInsufficientBuffer = 122;
    private const int ErrorSemTimeout = 121;
    private const int ErrorBadLength = 24;
    private const int ErrorIoPending = 997;
    private const int ErrorOperationAborted = 995;
    private const int ErrorConnectionRefused = 1225;
    private const int ErrorInvalidUserBuffer = 1784;
    private const int UsbipErrorAbi = unchecked((int)0xE1000005);

    private static volatile int _detectedAbi;

    public static string? GetVhciDevicePath()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        Guid guid = GuidDevInterfaceUsbipVhci;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            uint cr = CM_Get_Device_Interface_List_Size(out uint len, ref guid, IntPtr.Zero, CmGetDeviceInterfaceListPresent);
            if (cr != CrSuccess || len <= 1)
                return null;

            var buffer = new char[len];
            cr = CM_Get_Device_Interface_List(ref guid, IntPtr.Zero, buffer, len, CmGetDeviceInterfaceListPresent);
            if (cr == CrBufferSmall)
                continue;

            if (cr != CrSuccess)
                return null;

            int firstNull = Array.IndexOf(buffer, '\0');
            var path = firstNull > 0 ? new string(buffer, 0, firstNull) : string.Empty;
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }

        return null;
    }

    public static SafeFileHandle? OpenVhciHandle(bool overlapped = false)
    {
        var path = GetVhciDevicePath();
        if (string.IsNullOrEmpty(path))
            return null;

        uint flags = FileAttributeNormal | (overlapped ? FileFlagOverlapped : 0u);
        var handle = CreateFile(
            path,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            flags,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        return handle;
    }

    public static List<ConnectedDevice>? TryGetImportedDevices(IUsbIdsService? usbIds = null)
    {
        using var handle = OpenVhciHandle(overlapped: false);
        if (handle == null)
            return null;

        const int maxDevices = 1024;
        for (int capacity = 8; capacity <= maxDevices; capacity <<= 1)
        {
            var buffer = new byte[ImportedDeviceHeaderSize + capacity * ImportedDeviceRecordSize];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), GetImportedDevicesInitialSelfSize);

            if (DeviceIoControl(
                    handle,
                    IoctlGetImportedDevices,
                    buffer,
                    4,
                    buffer,
                    (uint)buffer.Length,
                    out uint bytesReturned,
                    IntPtr.Zero))
            {
                return ParseImportedDevicesBuffer(buffer, (int)bytesReturned, usbIds);
            }

            int err = Marshal.GetLastWin32Error();
            if (err != ErrorInsufficientBuffer)
                return null;
        }

        return null;
    }

    public static (bool InterfaceAvailable, bool ShouldFallbackToCli, int Port, int Win32Error, string? ErrorMessage) TryAttachDevice(
        string host,
        string busId,
        bool wskEvents,
        bool once = true,
        string? serial = null)
    {
        using var handle = OpenVhciHandle(overlapped: false);
        if (handle == null)
            return (false, true, 0, Marshal.GetLastWin32Error(), null);

        uint ioctl = once ? IoctlPluginHardwareOnce : IoctlPluginHardware;
        var primaryAbi = _detectedAbi == (int)VhciAbiVersion.V0982 ? VhciAbiVersion.V0982 : VhciAbiVersion.V0981;
        var secondaryAbi = primaryAbi == VhciAbiVersion.V0981 ? VhciAbiVersion.V0982 : VhciAbiVersion.V0981;

        var res = InvokeAttachIoctl(handle, ioctl, host, busId, wskEvents, serial, primaryAbi);
        if (res.Port > 0)
        {
            _detectedAbi = (int)primaryAbi;
            return (true, false, res.Port, 0, null);
        }

        if (IsAbiMismatchError(res.Win32Error))
        {
            var retry = InvokeAttachIoctl(handle, ioctl, host, busId, wskEvents, serial, secondaryAbi);
            if (retry.Port > 0)
            {
                _detectedAbi = (int)secondaryAbi;
                return (true, false, retry.Port, 0, null);
            }
            res = retry;
        }

        bool fallback = res.Win32Error == ErrorAccessDenied || IsAbiMismatchError(res.Win32Error);
        return (true, fallback, 0, res.Win32Error, MapAttachError(res.Win32Error));
    }

    private static (int Port, int Win32Error) InvokeAttachIoctl(
        SafeFileHandle handle,
        uint ioctl,
        string host,
        string busId,
        bool wskEvents,
        string? serial,
        VhciAbiVersion abi)
    {
        byte[] input = BuildPluginPayload(host, busId, wskEvents, serial, DefaultServicePort, abi);
        var output = new byte[8];

        if (DeviceIoControl(
                handle,
                ioctl,
                input,
                (uint)input.Length,
                output,
                (uint)output.Length,
                out uint bytesReturned,
                IntPtr.Zero))
        {
            if (bytesReturned == 8)
            {
                int port = BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(4, 4));
                if (port > 0)
                    return (port, 0);
            }
            return (0, ErrorInvalidParameter);
        }

        return (0, Marshal.GetLastWin32Error());
    }

    public static (bool InterfaceAvailable, bool Success, int Win32Error) TryDetachPort(int port)
    {
        using var handle = OpenVhciHandle(overlapped: false);
        if (handle == null)
            return (false, false, Marshal.GetLastWin32Error());

        byte[] payload = BuildPlugoutPayload(port);
        if (DeviceIoControl(
                handle,
                IoctlPlugoutHardware,
                payload,
                (uint)payload.Length,
                null,
                0,
                out _,
                IntPtr.Zero))
        {
            return (true, true, 0);
        }

        return (true, false, Marshal.GetLastWin32Error());
    }

    public static (bool InterfaceAvailable, int CanceledCount, int Win32Error) TryStopAttachAttempts(string? host = null, string? busId = null)
    {
        using var handle = OpenVhciHandle(overlapped: false);
        if (handle == null)
            return (false, 0, Marshal.GetLastWin32Error());

        var primaryAbi = _detectedAbi == (int)VhciAbiVersion.V0982 ? VhciAbiVersion.V0982 : VhciAbiVersion.V0981;
        var secondaryAbi = primaryAbi == VhciAbiVersion.V0981 ? VhciAbiVersion.V0982 : VhciAbiVersion.V0981;

        var res = InvokeStopAttachIoctl(handle, host, busId, primaryAbi);
        if (res.Success)
        {
            _detectedAbi = (int)primaryAbi;
            return (true, res.Count, 0);
        }

        if (IsAbiMismatchError(res.Win32Error))
        {
            var retry = InvokeStopAttachIoctl(handle, host, busId, secondaryAbi);
            if (retry.Success)
            {
                _detectedAbi = (int)secondaryAbi;
                return (true, retry.Count, 0);
            }
            res = retry;
        }

        return (true, 0, res.Win32Error);
    }

    private static (bool Success, int Count, int Win32Error) InvokeStopAttachIoctl(
        SafeFileHandle handle,
        string? host,
        string? busId,
        VhciAbiVersion abi)
    {
        byte[] payload = BuildStopAttachPayload(host, busId, DefaultServicePort, abi);
        if (DeviceIoControl(
                handle,
                IoctlStopAttachAttempts,
                payload,
                (uint)payload.Length,
                payload,
                (uint)payload.Length,
                out uint bytesReturned,
                IntPtr.Zero) && bytesReturned == payload.Length)
        {
            int countOffset = abi == VhciAbiVersion.V0981 ? 0x450 : 4;
            int count = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(countOffset, 4));
            return (true, Math.Max(0, count), 0);
        }

        return (false, 0, Marshal.GetLastWin32Error());
    }

    /// <summary>
    /// Listens asynchronously for kernel push events on an overlapped VHCI device handle
    /// until cancelled or until the driver interface stops existing.
    /// </summary>
    public static async Task RunDeviceStateListenerAsync(
        Action<VhciDeviceStateEvent> onEvent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(onEvent);
        if (!OperatingSystem.IsWindows())
            return;

        await Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                using var handle = OpenVhciHandle(overlapped: true);
                if (handle == null)
                {
                    if (ct.WaitHandle.WaitOne(2000))
                        break;
                    continue;
                }

                ReadDeviceStateLoop(handle, onEvent, ct);

                if (!ct.IsCancellationRequested)
                    ct.WaitHandle.WaitOne(500);
            }
        }, CancellationToken.None).ConfigureAwait(false);
    }

    private static void ReadDeviceStateLoop(
        SafeFileHandle handle,
        Action<VhciDeviceStateEvent> onEvent,
        CancellationToken ct)
    {
        var buffer = new byte[DeviceStatePacketSize];
        using var ioEvent = new ManualResetEvent(false);
        var waitHandles = new WaitHandle[] { ioEvent, ct.WaitHandle };

        GCHandle pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            IntPtr bufPtr = pinned.AddrOfPinnedObject();
            while (!ct.IsCancellationRequested)
            {
                ioEvent.Reset();
                var overlapped = new NativeOverlapped
                {
                    OffsetLow = 0,
                    OffsetHigh = 0,
                    EventHandle = ioEvent.SafeWaitHandle.DangerousGetHandle()
                };

                bool immediate = ReadFile(handle, bufPtr, (uint)buffer.Length, out uint bytesRead, ref overlapped);
                if (!immediate)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err != ErrorIoPending)
                        break;

                    int signaled = WaitHandle.WaitAny(waitHandles);
                    if (signaled == 1 || ct.IsCancellationRequested)
                    {
                        CancelIoEx(handle, ref overlapped);
                        // Wait for IRP completion while pinned buffer is still valid.
                        GetOverlappedResult(handle, ref overlapped, out _, bWait: true);
                        break;
                    }

                    if (!GetOverlappedResult(handle, ref overlapped, out bytesRead, bWait: false))
                    {
                        int ovErr = Marshal.GetLastWin32Error();
                        if (ovErr == ErrorOperationAborted)
                            break;
                        break;
                    }
                }

                if (bytesRead == 0)
                    break;

                if (bytesRead == DeviceStatePacketSize)
                {
                    var evt = ParseDeviceStatePacket(buffer);
                    if (evt != null)
                        onEvent(evt);
                }
            }
        }
        finally
        {
            if (pinned.IsAllocated)
                pinned.Free();
        }
    }

    public static byte[] BuildPluginPayload(
        string host,
        string busId,
        bool wskEvents,
        string? serial = null,
        string service = DefaultServicePort,
        VhciAbiVersion abi = VhciAbiVersion.V0981)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host must not be empty.", nameof(host));
        if (string.IsNullOrWhiteSpace(busId))
            throw new ArgumentException("Bus ID must not be empty.", nameof(busId));

        int size = abi == VhciAbiVersion.V0981 ? PluginHardwareSizeV0981 : PluginHardwareSizeV0982;
        var buf = new byte[size];
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0, 4), (uint)size);

        // Both v0.9.8.1 and v0.9.8.2 share identical location offsets inside plugin_hardware:
        // 0x0C: busid[32], 0x2C: service[32], 0x4C: host[1025].
        WriteAsciiZ(buf.AsSpan(0x00C, BusIdSize), busId);
        WriteAsciiZ(buf.AsSpan(0x02C, ServiceSize), string.IsNullOrWhiteSpace(service) ? DefaultServicePort : service);
        WriteAsciiZ(buf.AsSpan(0x04C, HostSize), host);

        int serialOffset = abi == VhciAbiVersion.V0981 ? 0x450 : 0x44D;
        int wskOffset = abi == VhciAbiVersion.V0981 ? 0x460 : 0x45D;

        if (!string.IsNullOrEmpty(serial))
            WriteAsciiZ(buf.AsSpan(serialOffset, SerialBufSize), serial);

        buf[wskOffset] = wskEvents ? (byte)1 : (byte)0;
        return buf;
    }

    public static byte[] BuildStopAttachPayload(
        string? host = null,
        string? busId = null,
        string service = DefaultServicePort,
        VhciAbiVersion abi = VhciAbiVersion.V0981)
    {
        int size = abi == VhciAbiVersion.V0981 ? StopAttachAttemptsSizeV0981 : StopAttachAttemptsSizeV0982;
        var buf = new byte[size];
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0, 4), (uint)size);

        if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(busId))
        {
            int busIdOffset = abi == VhciAbiVersion.V0981 ? 0x00C : 0x008;
            int serviceOffset = abi == VhciAbiVersion.V0981 ? 0x02C : 0x028;
            int hostOffset = abi == VhciAbiVersion.V0981 ? 0x04C : 0x048;

            WriteAsciiZ(buf.AsSpan(busIdOffset, BusIdSize), busId);
            WriteAsciiZ(buf.AsSpan(serviceOffset, ServiceSize), string.IsNullOrWhiteSpace(service) ? DefaultServicePort : service);
            WriteAsciiZ(buf.AsSpan(hostOffset, HostSize), host);
        }

        return buf;
    }

    public static byte[] BuildPlugoutPayload(int port)
    {
        var buf = new byte[PlugoutHardwareSize];
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0, 4), PlugoutHardwareSize);
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4, 4), port);
        return buf;
    }

    public static List<ConnectedDevice>? ParseImportedDevicesBuffer(
        ReadOnlySpan<byte> data,
        int bytesReturned,
        IUsbIdsService? usbIds = null)
    {
        if (bytesReturned < ImportedDeviceHeaderSize || bytesReturned > data.Length)
            return null;

        int payloadBytes = bytesReturned - ImportedDeviceHeaderSize;
        if (payloadBytes % ImportedDeviceRecordSize != 0)
            return null;

        int count = payloadBytes / ImportedDeviceRecordSize;
        var devices = new List<ConnectedDevice>(count);

        for (int i = 0; i < count; i++)
        {
            var rec = data.Slice(ImportedDeviceHeaderSize + i * ImportedDeviceRecordSize, ImportedDeviceRecordSize);
            devices.Add(ParseImportedDeviceRecord(rec, usbIds).Device);
        }

        return devices;
    }

    public static VhciDeviceStateEvent? ParseDeviceStatePacket(
        ReadOnlySpan<byte> data,
        IUsbIdsService? usbIds = null)
    {
        if (data.Length < DeviceStatePacketSize)
            return null;

        uint reportedSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4));
        if (reportedSize != DeviceStatePacketSize)
            return null;

        bool isV0982 = LooksLikeV0982DeviceState(data);
        int stateRaw;
        uint sourceId;
        ReadOnlySpan<byte> record;

        if (isV0982)
        {
            _detectedAbi = (int)VhciAbiVersion.V0982;
            stateRaw = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));
            sourceId = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
            record = data.Slice(12, ImportedDeviceRecordSize);
        }
        else
        {
            _detectedAbi = (int)VhciAbiVersion.V0981;
            record = data.Slice(4, ImportedDeviceRecordSize);
            stateRaw = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x470, 4));
            sourceId = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x474, 4));
        }

        if (stateRaw < (int)VhciDeviceState.Unplugged || stateRaw > (int)VhciDeviceState.Unplugging)
            return null;

        var (device, recvMode) = ParseImportedDeviceRecord(record, usbIds, forceAbi: isV0982 ? VhciAbiVersion.V0982 : VhciAbiVersion.V0981);
        return new VhciDeviceStateEvent((VhciDeviceState)stateRaw, sourceId, device, recvMode);
    }

    private static (ConnectedDevice Device, string ReceiveMode) ParseImportedDeviceRecord(
        ReadOnlySpan<byte> rec,
        IUsbIdsService? usbIds,
        VhciAbiVersion? forceAbi = null)
    {
        int port = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(0, 4));
        string busId = ReadAsciiZ(rec.Slice(0x008, BusIdSize));
        string host = ReadAsciiZ(rec.Slice(0x048, HostSize));

        bool isV0982 = forceAbi.HasValue
            ? forceAbi.Value == VhciAbiVersion.V0982
            : LooksLikeV0982ImportedDevice(rec);

        ushort vendor;
        ushort product;
        bool wskEvents;

        if (isV0982)
        {
            _detectedAbi = (int)VhciAbiVersion.V0982;
            wskEvents = rec[0x459] != 0;
            vendor = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(0x464, 2));
            product = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(0x466, 2));
        }
        else
        {
            _detectedAbi = (int)VhciAbiVersion.V0981;
            vendor = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(0x454, 2));
            product = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(0x456, 2));
            wskEvents = rec[0x469] != 0;
        }

        string vid = vendor.ToString("x4");
        string pid = product.ToString("x4");
        string desc = usbIds?.GetDeviceDescription(vid, pid) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(desc))
            desc = $"{vid}:{pid}";

        var dev = new ConnectedDevice
        {
            Port = port,
            BusId = busId,
            ServerIP = host,
            VendorId = vid,
            ProductId = pid,
            Description = desc
        };

        return (dev, wskEvents ? "low-latency" : "zero-copy");
    }

    private static bool LooksLikeV0982ImportedDevice(ReadOnlySpan<byte> rec)
    {
        uint devid981 = BinaryPrimitives.ReadUInt32LittleEndian(rec.Slice(0x44C, 4));
        int speed981 = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(0x450, 4));
        ushort vendor981 = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(0x454, 2));
        ushort product981 = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(0x456, 2));

        uint devid982 = BinaryPrimitives.ReadUInt32LittleEndian(rec.Slice(0x45C, 4));
        int speed982 = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(0x460, 4));
        ushort vendor982 = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(0x464, 2));
        ushort product982 = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(0x466, 2));

        bool plausible981 = devid981 != 0 && speed981 is >= 0 and <= 6 && vendor981 != 0 && product981 != 0
                            && IsZeroOrAsciiAlnum(rec.Slice(0x458, SerialBufSize))
                            && rec[0x469] <= 1;

        bool plausible982 = devid982 != 0 && speed982 is >= 0 and <= 6 && vendor982 != 0 && product982 != 0
                            && IsZeroOrAsciiAlnum(rec.Slice(0x449, SerialBufSize))
                            && rec[0x459] <= 1;

        if (plausible982 && !plausible981)
            return true;
        if (plausible981 && !plausible982)
            return false;

        return _detectedAbi == (int)VhciAbiVersion.V0982;
    }

    private static bool LooksLikeV0982DeviceState(ReadOnlySpan<byte> data)
    {
        int state981 = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x470, 4));
        int state982 = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4));

        bool state981Valid = state981 is >= 0 and <= 5;
        bool state982Valid = state982 is >= 0 and <= 5;

        if (state982Valid && !state981Valid)
            return true;
        if (state981Valid && !state982Valid)
            return false;

        // Disambiguate when both look like valid enum integers: check busid string location
        string bus981 = ReadAsciiZ(data.Slice(4 + 0x008, BusIdSize));
        string bus982 = ReadAsciiZ(data.Slice(12 + 0x008, BusIdSize));
        bool validBus981 = bus981.Contains('-') && bus981.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '.');
        bool validBus982 = bus982.Contains('-') && bus982.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '.');

        if (validBus982 && !validBus981)
            return true;
        if (validBus981 && !validBus982)
            return false;

        return LooksLikeV0982ImportedDevice(data.Slice(12, ImportedDeviceRecordSize));
    }

    private static bool IsZeroOrAsciiAlnum(ReadOnlySpan<byte> buf)
    {
        for (int i = 0; i < buf.Length; i++)
        {
            byte b = buf[i];
            if (b == 0)
                return true;
            if (!((b >= '0' && b <= '9') || (b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z')))
                return false;
        }
        return false; // missing NUL terminator
    }

    private static void WriteAsciiZ(Span<byte> dest, string value)
    {
        dest.Clear();
        if (string.IsNullOrEmpty(value))
            return;

        int maxContent = dest.Length - 1;
        int written = Encoding.UTF8.GetBytes(value.AsSpan(), dest[..maxContent]);
        dest[written] = 0;
    }

    private static string ReadAsciiZ(ReadOnlySpan<byte> src)
    {
        int nullIdx = src.IndexOf((byte)0);
        var slice = nullIdx >= 0 ? src[..nullIdx] : src;
        return Encoding.UTF8.GetString(slice);
    }

    private static bool IsAbiMismatchError(int err) =>
        err == ErrorInvalidParameter ||
        err == ErrorInsufficientBuffer ||
        err == ErrorBadLength ||
        err == ErrorInvalidUserBuffer ||
        err == UsbipErrorAbi;

    private static string MapAttachError(int err) => err switch
    {
        ErrorSemTimeout or 10060 => $"VHCI attach timed out connecting to remote server (error {err}).",
        ErrorConnectionRefused or 10061 => $"VHCI attach failed: connection refused by remote USB/IP daemon (error {err}).",
        10051 => $"VHCI attach failed: remote network unreachable (error {err}).",
        unchecked((int)0xE1010001) => "VHCI attach failed: device not available on remote host (ST_NA).",
        unchecked((int)0xE1010002) => "VHCI attach failed: device is already in use (ST_DEV_BUSY).",
        unchecked((int)0xE1010003) => "VHCI attach failed: remote USB/IP device error (ST_DEV_ERR).",
        unchecked((int)0xE1010004) => "VHCI attach failed: requested busid not found on server (ST_NODEV).",
        unchecked((int)0xE1000006) => "VHCI attach failed: invalid USB serial string.",
        _ => $"VHCI attach failed with Win32 error 0x{err:X8} ({new Win32Exception(err).Message})."
    };

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_Interface_List_Size(
        out uint pulLen,
        ref Guid interfaceClassGuid,
        IntPtr pDeviceID,
        uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_Interface_List(
        ref Guid interfaceClassGuid,
        IntPtr pDeviceID,
        [Out] char[] buffer,
        uint bufferLen,
        uint ulFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        byte[] lpInBuffer,
        uint nInBufferSize,
        byte[]? lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(
        SafeFileHandle hFile,
        IntPtr lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        ref NativeOverlapped lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOverlappedResult(
        SafeFileHandle hFile,
        ref NativeOverlapped lpOverlapped,
        out uint lpNumberOfBytesTransferred,
        [MarshalAs(UnmanagedType.Bool)] bool bWait);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIoEx(
        SafeFileHandle hFile,
        ref NativeOverlapped lpOverlapped);
}
