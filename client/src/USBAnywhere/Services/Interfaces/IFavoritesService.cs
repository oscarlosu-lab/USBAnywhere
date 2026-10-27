// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025 SnakeFoxu - GPL v3

using USBAnywhere.Models;

namespace USBAnywhere.Services.Interfaces;

public interface IFavoritesService
{
    List<FavoriteDevice> GetFavorites();
    OperationResult AddFavorite(string serverIP, string busId, string description, bool autoConnect = true);
    OperationResult RemoveFavorite(string serverIP, string busId);
    bool IsFavorite(string serverIP, string busId);
    Task<List<FavoriteConnectResult>> ConnectFavoritesAsync();
}
