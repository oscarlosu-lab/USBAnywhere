// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025 SnakeFoxu - GPL v3

using USBAnywhere.Models;

namespace USBAnywhere.Services.Interfaces;

public interface IUpdateService
{
    Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken ct = default);
    Task<OperationResult> StartUpdateAsync(
        string downloadUrl,
        string installerName,
        string? hashUrl = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}
