using System;
using System.IO;
using USBAnywhere.Models;
using USBAnywhere.Services;
using USBAnywhere.Services.Interfaces;
using Xunit;

namespace USBAnywhere.Tests;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configFilePath;
    private readonly MockEnvironmentService _mockEnv;
    private readonly MockLoggerService _mockLogger;

    public ConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "USBAnywhere_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configFilePath = Path.Combine(_tempDir, "config.json");
        _mockEnv = new MockEnvironmentService(_configFilePath);
        _mockLogger = new MockLoggerService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void Load_ReturnsDefaultConfig_WhenFileDoesNotExist()
    {
        var service = new ConfigService(_mockEnv, _mockLogger);

        var config = service.Load();

        Assert.NotNull(config);
        Assert.NotNull(config.Favorites);
        Assert.NotNull(config.CustomDeviceNames);
        Assert.Contains(config.Theme, new[] { "dark", "light" });
    }

    [Fact]
    public void Load_ReturnsDefaultConfig_WhenFileIsCorrupted()
    {
        File.WriteAllText(_configFilePath, "INVALID_NOT_A_JSON_CONTENT{{{");
        var service = new ConfigService(_mockEnv, _mockLogger);

        var config = service.Load();

        Assert.NotNull(config);
        Assert.NotNull(config.Favorites);
    }

    [Fact]
    public void Save_PersistsConfigurationAtomically()
    {
        var service = new ConfigService(_mockEnv, _mockLogger);
        var original = new AppConfig
        {
            Language = "es",
            Theme = "light",
            AutoConnectFavorites = true,
            DefaultReceiveMode = "low-latency",
            AutoDetachOnExit = true
        };

        service.Save(original);

        Assert.True(File.Exists(_configFilePath));
        Assert.False(File.Exists(_configFilePath + ".tmp"));

        var loaded = service.Load();
        Assert.Equal("es", loaded.Language);
        Assert.Equal("light", loaded.Theme);
        Assert.True(loaded.AutoConnectFavorites);
        Assert.Equal("low-latency", loaded.DefaultReceiveMode);
        Assert.True(loaded.AutoDetachOnExit);
    }

    [Fact]
    public void SetLanguage_UpdatesAndSavesConfiguration()
    {
        var service = new ConfigService(_mockEnv, _mockLogger);

        service.SetLanguage("en");

        var loaded = service.Load();
        Assert.Equal("en", loaded.Language);
    }

    private class MockEnvironmentService : IEnvironmentService
    {
        private readonly string _configPath;
        public MockEnvironmentService(string configPath) => _configPath = configPath;

        public string AppDataPath => Path.GetDirectoryName(_configPath) ?? "";
        public string LogsPath => Path.Combine(AppDataPath, "logs");
        public string TempPath => Path.Combine(AppDataPath, "temp");
        public string BinPath => AppDataPath;
        public string DriversPath => AppDataPath;
        public bool IsPortableMode => false;
        public bool IsPortable => false;

        public void Initialize() { }
        public string GetConfigFilePath() => _configPath;
    }

    private class MockLoggerService : ILoggerService
    {
        public void Initialize() { }
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? ex = null) { }
    }
}
