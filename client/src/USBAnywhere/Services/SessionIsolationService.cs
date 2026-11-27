using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace USBAnywhere.Services;

public sealed record SessionSnapshot(
    int CurrentSessionId,
    uint ActiveConsoleSessionId,
    int ActiveSessionCount,
    bool IsCurrentSessionActive,
    bool IsMultiSessionHost);

/// <summary>
/// Isolates attached USB/IP ports and auto-connect actions across concurrent Windows user sessions (RDP / multi-seat).
/// </summary>
public sealed class SessionIsolationService
{
    private const int WtsActive = 0;
    private readonly ConcurrentDictionary<int, int> _portOwners = new();

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessionsW(
        IntPtr hServer,
        int reserved,
        int version,
        out IntPtr ppSessionInfo,
        out int pCount);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pointer);

    [StructLayout(LayoutKind.Sequential)]
    private struct WtsSessionInfo
    {
        public int SessionId;
        public IntPtr pWinStationName;
        public int State;
    }

    public static SessionSnapshot GetCurrentSessionInfo()
    {
        int currentId = Process.GetCurrentProcess().SessionId;
        uint consoleId = 0;
        try { consoleId = WTSGetActiveConsoleSessionId(); } catch { }

        int activeCount = 0;
        bool currentActive = currentId >= 0 && (uint)currentId == consoleId;

        try
        {
            if (WTSEnumerateSessionsW(IntPtr.Zero, 0, 1, out IntPtr pInfo, out int count))
            {
                try
                {
                    int structSize = Marshal.SizeOf<WtsSessionInfo>();
                    for (int i = 0; i < count; i++)
                    {
                        var info = Marshal.PtrToStructure<WtsSessionInfo>(IntPtr.Add(pInfo, i * structSize));
                        if (info.State == WtsActive)
                        {
                            activeCount++;
                            if (info.SessionId == currentId)
                                currentActive = true;
                        }
                    }
                }
                finally
                {
                    WTSFreeMemory(pInfo);
                }
            }
        }
        catch
        {
            activeCount = Math.Max(activeCount, currentActive ? 1 : 0);
        }

        return new SessionSnapshot(
            CurrentSessionId: currentId,
            ActiveConsoleSessionId: consoleId,
            ActiveSessionCount: Math.Max(1, activeCount),
            IsCurrentSessionActive: currentActive,
            IsMultiSessionHost: activeCount > 1);
    }

    public static bool EvaluateAutoConnect(bool isCurrentSessionActive, bool enforceIsolation)
    {
        if (!enforceIsolation) return true;
        return isCurrentSessionActive;
    }

    public static bool ShouldAllowAutoConnect(SessionSnapshot snap, bool enforceIsolation)
        => EvaluateAutoConnect(snap.IsCurrentSessionActive, enforceIsolation);

    public void RegisterPortOwner(int port, int? sessionId = null)
    {
        if (port <= 0) return;
        _portOwners[port] = sessionId ?? Process.GetCurrentProcess().SessionId;
    }

    public void ReleasePortOwner(int port)
    {
        _portOwners.TryRemove(port, out _);
    }

    public bool CanSessionControlPort(int port, int? sessionId = null, bool enforceIsolation = true)
    {
        if (!enforceIsolation) return true;
        int caller = sessionId ?? Process.GetCurrentProcess().SessionId;
        return !_portOwners.TryGetValue(port, out int owner) || owner == caller;
    }

    public int? GetPortOwner(int port)
        => _portOwners.TryGetValue(port, out int owner) ? owner : null;
}
