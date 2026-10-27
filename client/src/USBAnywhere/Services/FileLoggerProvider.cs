// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025 SnakeFoxu - GPL v3

using System;
using Microsoft.Extensions.Logging;
using USBAnywhere.Services.Interfaces;

namespace USBAnywhere.Services;

/// <summary>
/// Bridges Microsoft.Extensions.Logging (ILogger<T>) to our file-based ILoggerService
/// ensuring all service and security logs are persisted to disk in WinExe mode.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ILoggerService _loggerService;

    public FileLoggerProvider(ILoggerService loggerService)
    {
        _loggerService = loggerService;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLoggerBridge(categoryName, _loggerService);
    }

    public void Dispose() { }

    private sealed class FileLoggerBridge : ILogger
    {
        private readonly string _category;
        private readonly ILoggerService _fileLogger;

        public FileLoggerBridge(string category, ILoggerService fileLogger)
        {
            var shortCat = category;
            var lastDot = category.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < category.Length - 1)
            {
                shortCat = category[(lastDot + 1)..];
            }
            _category = shortCat;
            _fileLogger = fileLogger;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var msg = $"[{_category}] {formatter(state, exception)}";
            switch (logLevel)
            {
                case LogLevel.Critical:
                case LogLevel.Error:
                    _fileLogger.Error(msg, exception);
                    break;
                case LogLevel.Warning:
                    _fileLogger.Warning(msg);
                    break;
                case LogLevel.Debug:
                case LogLevel.Trace:
                    _fileLogger.Debug(msg);
                    break;
                default:
                    _fileLogger.Info(msg);
                    break;
            }
        }
    }
}
