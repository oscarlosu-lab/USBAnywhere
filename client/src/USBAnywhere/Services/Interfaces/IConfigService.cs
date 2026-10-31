// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025-2026 SnakeFoxu (OscarLosu) - GPL v3

using USBAnywhere.Models;

namespace USBAnywhere.Services.Interfaces;

public interface IConfigService
{
    AppConfig Load();
    void Save(AppConfig config);
    void SetLanguage(string language);
}
