using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using USBAnywhere.Services;
using USBAnywhere.Services.Interfaces;
using USBAnywhere.ViewModels;

namespace USBAnywhere;

/// <summary>
/// App entry — Auto-elevation + Single instance (Mutex) + DI container
/// Uses programmatic elevation to avoid .NET 9 WPF crash with requireAdministrator manifest.
/// </summary>
public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private static bool _ownsMutex;
    private const string MutexName = "USBAnywhere_SingleInstance_Mutex";
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // ── STEP 0: Global Unhandled Exception Handlers ──────────────────
        DispatcherUnhandledException += (s, args) =>
        {
            var logger = ServiceProvider?.GetService<ILoggerService>();
            logger?.Error("Unhandled Dispatcher Exception", args.Exception);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var logger = ServiceProvider?.GetService<ILoggerService>();
            if (args.ExceptionObject is Exception ex)
            {
                logger?.Error("Unhandled AppDomain Exception", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            var logger = ServiceProvider?.GetService<ILoggerService>();
            logger?.Error("Unobserved Task Exception", args.Exception);
            args.SetObserved();
        };

        try
        {
            // ── STEP 1: Auto-elevate if not admin ──────────────────────────
            if (!IsRunningAsAdmin())
            {
                try
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName
                                  ?? System.Environment.ProcessPath
                                  ?? throw new InvalidOperationException("Cannot determine executable path.");

                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = string.Join(" ", e.Args.Select(a => $"\"{a}\"")),
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // User declined UAC — exit silently
                }
                catch (Exception ex)
                {
                    bool isEs = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase);
                    string adminMsg = isEs
                        ? $"No se pudo elevar a administrador:\n{ex.Message}"
                        : $"Could not elevate to administrator:\n{ex.Message}";
                    System.Windows.MessageBox.Show(adminMsg, "USBAnywhere", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                Shutdown();
                return;
            }

            // ── STEP 2: Single instance guard ──────────────────────────────
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            _ownsMutex = createdNew;
            if (!createdNew)
            {
                bool isEs = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase);
                string runningMsg = isEs
                    ? "USBAnywhere ya está en ejecución.\nRevisa la bandeja del sistema."
                    : "USBAnywhere is already running.\nCheck the system tray.";
                System.Windows.MessageBox.Show(runningMsg, "USBAnywhere", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // ── STEP 3: Build DI container ─────────────────────────────────
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Initialize core services — order matters
            ServiceProvider.GetRequiredService<IEnvironmentService>().Initialize();
            ServiceProvider.GetRequiredService<ILoggerService>().Initialize();
            
            // Create and show MainWindow via DI
            var configService = ServiceProvider.GetRequiredService<IConfigService>();
            var appConfig = configService.Load();

            bool startMinimized = appConfig.StartMinimized
                || e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) || a.Equals("-m", StringComparison.OrdinalIgnoreCase));

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            if (startMinimized)
            {
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.Hide();
                _ = mainWindow.EnsureStartedAsync();
            }
            else
            {
                mainWindow.Show();
            }

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Critical Startup Error: {ex.Message}\n\n{ex.InnerException?.Message}\n\n{ex.StackTrace}", 
                "Fatal Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core infrastructure
        services.AddSingleton<IEnvironmentService, EnvironmentService>();
        services.AddSingleton<ILoggerService, LoggerService>();
        services.AddSingleton<IConfigService, ConfigService>();

        // Microsoft.Extensions.Logging (bridged to file logger for WinExe mode)
        services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
        services.AddLogging(b =>
        {
            b.AddConsole();
            b.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
            b.SetMinimumLevel(LogLevel.Information);
        });

        // Domain services
        services.AddSingleton<IUsbipService, UsbipService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IVpnService, VpnService>();
        services.AddSingleton<IDriverService, DriverService>();
        services.AddSingleton<IUsbIdsService, UsbIdsService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IFavoritesService, FavoritesService>();
        services.AddSingleton<IConnectionMonitorService, ConnectionMonitorService>();
        services.AddSingleton<INetworkScanner, NetworkScanner>();
        services.AddSingleton<INotificationService, NotificationService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();

        // UI
        services.AddTransient<MainWindow>();
    }

    /// <summary>
    /// Check if the current process is running with administrator privileges.
    /// </summary>
    private static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { if (_ownsMutex) _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

