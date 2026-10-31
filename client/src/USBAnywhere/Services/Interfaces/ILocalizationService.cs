// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

namespace USBAnywhere.Services.Interfaces;

public interface ILocalizationService
{
    string GetText(string key, string language);
}
