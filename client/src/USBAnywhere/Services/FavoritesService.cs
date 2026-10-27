using System.Text.Json;
using USBAnywhere.Models;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// Favorites service - Manage favorite devices with auto-connect
/// Port from PowerShell Add-Favorite / Remove-Favorite / Get-Favorites / Test-IsFavorite / Connect-Favorites
/// </summary>
public class FavoritesService : IFavoritesService
{
    private readonly IConfigService _config;
    private readonly IUsbipService _usbip;

    public FavoritesService(IConfigService config, IUsbipService usbip)
    {
        _config = config;
        _usbip = usbip;
    }
    
    public List<FavoriteDevice> GetFavorites()
    {
        var config = _config.Load();
        return config.Favorites ?? new List<FavoriteDevice>();
    }
    
    public OperationResult AddFavorite(string serverIP, string busId, string description, bool autoConnect = true)
    {
        var config = _config.Load();
        config.Favorites ??= new List<FavoriteDevice>();
        
        // Check if already exists
        if (config.Favorites.Any(f => f.ServerIP == serverIP && f.BusId == busId))
            return new OperationResult { Success = false, Error = "Already in favorites" };
        
        // Add favorite
        config.Favorites.Add(new FavoriteDevice
        {
            ServerIP = serverIP,
            BusId = busId,
            Description = description,
            AutoConnect = autoConnect,
            AddedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
        
        _config.Save(config);
        return new OperationResult { Success = true };
    }
    
    public OperationResult RemoveFavorite(string serverIP, string busId)
    {
        var config = _config.Load();
        config.Favorites ??= new List<FavoriteDevice>();
        
        var toRemove = config.Favorites.FirstOrDefault(f => f.ServerIP == serverIP && f.BusId == busId);
        if (toRemove != null)
        {
            config.Favorites.Remove(toRemove);
            _config.Save(config);
            return new OperationResult { Success = true };
        }
        
        return new OperationResult { Success = false, Error = "Not found in favorites" };
    }
    
    public bool IsFavorite(string serverIP, string busId)
    {
        var favorites = GetFavorites();
        return favorites.Any(f => f.ServerIP == serverIP && f.BusId == busId);
    }
    
    public async Task<List<FavoriteConnectResult>> ConnectFavoritesAsync()
    {
        var results = new List<FavoriteConnectResult>();
        var favorites = GetFavorites().Where(f => f.AutoConnect).ToList();
        
        foreach (var fav in favorites)
        {
            var result = await _usbip.ConnectAsync(fav.ServerIP, fav.BusId);
            results.Add(new FavoriteConnectResult
            {
                Device = $"{fav.ServerIP}/{fav.BusId}",
                Success = result.Success,
                Error = result.Error
            });
        }
        
        return results;
    }
}
