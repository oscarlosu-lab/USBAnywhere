// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025 SnakeFoxu - GPL v3

namespace USBAnywhere.Services.Interfaces;

public interface INetworkScanner
{
    Task<List<string>> ScanNetworkAsync(string subnetBase);
}
