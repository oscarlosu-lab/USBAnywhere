using System.Diagnostics;
using System.IO;
using USBAnywhere.Models;

namespace USBAnywhere.Services;

public sealed class StreamingSessionMonitor
{
    private readonly object _lock = new();
    private bool _isStreamActive;
    private string _activeProvider = string.Empty;

    public bool IsStreamActive { get { lock (_lock) return _isStreamActive; } }
    public string ActiveProvider { get { lock (_lock) return _activeProvider; } }

    public event EventHandler<string>? StreamStarted;
    public event EventHandler<string>? StreamStopped;

    public static string? DetectStreamingProvider(IEnumerable<string> runningProcessNames)
    {
        var names = new HashSet<string>(
            runningProcessNames.Select(n => Path.GetFileNameWithoutExtension(n).Trim()),
            StringComparer.OrdinalIgnoreCase);

        if (names.Contains("apollo")) return "apollo";
        if (names.Contains("vibepollo")) return "vibepollo";
        if (names.Contains("vibeshine")) return "vibeshine";
        if (names.Contains("sunshine")) return "sunshine";
        if (names.Contains("parsecd") || names.Contains("parsec")) return "parsec";
        if (names.Contains("streaming_client")) return "steam_remote_play";
        return null;
    }

    public void SetStreamState(bool active, string? provider = null)
    {
        var prov = string.IsNullOrWhiteSpace(provider) ? "sunshine" : provider.Trim().ToLowerInvariant();
        bool changed = false;
        lock (_lock)
        {
            if (_isStreamActive != active || (active && _activeProvider != prov))
            {
                _isStreamActive = active;
                _activeProvider = active ? prov : string.Empty;
                changed = true;
            }
        }
        if (changed)
        {
            if (active) StreamStarted?.Invoke(this, prov);
            else StreamStopped?.Invoke(this, prov);
        }
    }

    public static List<FavoriteDevice> FilterFavoritesForStreamTransition(
        IEnumerable<FavoriteDevice> favorites,
        bool streamActive)
    {
        return favorites
            .Where(f => string.Equals(f.AttachMode, "only-during-stream", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
