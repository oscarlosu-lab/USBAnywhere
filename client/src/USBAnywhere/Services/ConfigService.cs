using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using USBAnywhere.Models;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// Configuration service - Load/Save app settings with robust error handling.
/// </summary>
public class ConfigService : IConfigService
{
    private readonly IEnvironmentService _env;
    private readonly ILoggerService _logger;
    private readonly string _configPath;
    private readonly object _fileLock = new();
    
    public ConfigService(IEnvironmentService env, ILoggerService logger)
    {
        _env = env;
        _logger = logger;
        _configPath = _env.GetConfigFilePath();
    }
    
    public AppConfig Load()
    {
        lock (_fileLock)
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var config = JsonSerializer.Deserialize<AppConfig>(json);
                        if (config != null)
                        {
                            _logger.Debug("Configuration loaded successfully.");
                            return config;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load configuration from {_configPath}: {ex.Message}");
            }
            
            _logger.Info("Configuration file not found or corrupted. Loading defaults.");
            
            // Default config with system language detection
            var systemLang = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            return new AppConfig
            {
                Language = systemLang == "es" ? "es" : "en",
                Theme = "dark",
                Favorites = new List<FavoriteDevice>(),
                CustomDeviceNames = new Dictionary<string, string>()
            };
        }
    }
    
    public void Save(AppConfig config)
    {
        lock (_fileLock)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(config, options);
                
                string? dir = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var tempPath = _configPath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _configPath, overwrite: true);
                _logger.Debug("Configuration saved atomically.");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to save configuration to {_configPath}: {ex.Message}");
            }
        }
    }
    
    public void SetLanguage(string language)
    {
        var config = Load();
        config.Language = language;
        Save(config);
        _logger.Info($"Language changed to: {language}");
    }
}
