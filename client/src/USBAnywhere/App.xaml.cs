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
    public const string DisconnectAllEventName = @"Local\USBAnywhere_DisconnectAll_Event";
    public const string GracefulExitEventName = @"Local\USBAnywhere_GracefulExit_Event";
    public const string IpcPipeName = "USBAnywhere_IpcPipe";
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

            // ── STEP 1.5: Headless CLI commands (--disconnect-all / --disconnect / --quit) ──
            if (IsHeadlessCliInvocation(e.Args))
            {
                bool handled = Task.Run(() => TryHandleHeadlessCliAsync(e.Args)).GetAwaiter().GetResult();
                if (handled)
                {
                    Shutdown(0);
                    return;
                }
            }

            // ── STEP 2: Single instance guard (forward --connect/--attach if already running) ──
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            _ownsMutex = createdNew;
            if (!createdNew)
            {
                if (TryParseConnectArgs(e.Args, out string ipcIp, out string ipcBusId) &&
                    Task.Run(() => TrySendIpcConnectAsync(ipcIp, ipcBusId)).GetAwaiter().GetResult())
                {
                    Shutdown(0);
                    return;
                }

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

    internal static bool TryParseConnectArgs(string[]? args, out string ip, out string busId)
    {
        ip = string.Empty;
        busId = string.Empty;
        if (args == null || args.Length < 3) return false;
        for (int i = 0; i < args.Length - 2; i++)
        {
            if (args[i].Equals("--connect", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("--attach", StringComparison.OrdinalIgnoreCase))
            {
                string candIp = args[i + 1].Trim();
                string candBusId = args[i + 2].Trim();
                if (USBAnywhere.Utils.InputValidator.IsValidHost(candIp) &&
                    USBAnywhere.Utils.InputValidator.IsValidBusId(candBusId))
                {
                    ip = candIp;
                    busId = candBusId;
                    return true;
                }
            }
        }
        return false;
    }

    private static async Task<bool> TrySendIpcConnectAsync(string ip, string busId)
    {
        try
        {
            using var pipe = new System.IO.Pipes.NamedPipeClientStream(".", IpcPipeName, System.IO.Pipes.PipeDirection.Out);
            await pipe.ConnectAsync(1500);
            using var writer = new System.IO.StreamWriter(pipe, System.Text.Encoding.UTF8) { AutoFlush = true };
            await writer.WriteLineAsync($"CONNECT|{ip}|{busId}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[IPC Connect] {ex.Message}");
            return false;
        }
    }

    internal static bool IsHeadlessCliInvocation(string[]? args)
    {
        if (args == null || args.Length == 0) return false;
        return args.Any(a =>
            a.Equals("--disconnect-all", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--disconnect", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--quit", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--exit", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<bool> TryHandleHeadlessCliAsync(string[] args)
    {
        bool disconnectAll = args.Any(a => a.Equals("--disconnect-all", StringComparison.OrdinalIgnoreCase));
        bool quitApp = args.Any(a => a.Equals("--quit", StringComparison.OrdinalIgnoreCase) || a.Equals("--exit", StringComparison.OrdinalIgnoreCase));
        string? disconnectPort = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--disconnect", StringComparison.OrdinalIgnoreCase))
            {
                disconnectPort = args[i + 1];
                break;
            }
        }

        if (!disconnectAll && !quitApp && string.IsNullOrWhiteSpace(disconnectPort))
            return false;

        try
        {
            if (quitApp)
            {
                try
                {
                    if (EventWaitHandle.TryOpenExisting(GracefulExitEventName, out var exitEvent))
                    {
                        using (exitEvent)
                            exitEvent.Set();
                    }
                }
                catch { }
            }
            else if (disconnectAll)
            {
                try
                {
                    if (EventWaitHandle.TryOpenExisting(DisconnectAllEventName, out var disconnectEvent))
                    {
                        using (disconnectEvent)
                            disconnectEvent.Set();
                        await Task.Delay(75);
                    }
                }
                catch { }
            }

            var services = new ServiceCollection();
            ConfigureServices(services);
            using var sp = services.BuildServiceProvider();
            sp.GetRequiredService<IEnvironmentService>().Initialize();
            var logger = sp.GetRequiredService<ILoggerService>();
            logger.Initialize();
            sp.GetRequiredService<IConfigService>().Load();
            var usbip = sp.GetRequiredService<IUsbipService>();

            if (disconnectAll || quitApp)
            {
                int detached = await usbip.DetachAllConnectedDevicesAsync();
                logger.Info($"CLI headless {(quitApp ? "--quit" : "--disconnect-all")}: detached {detached} device(s).");
            }
            else if (!string.IsNullOrWhiteSpace(disconnectPort))
            {
                bool ok = await usbip.DetachDeviceAsync(disconnectPort.Trim());
                logger.Info($"CLI headless --disconnect {disconnectPort}: {(ok ? "OK" : "FAILED")}.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HeadlessCli] {ex.Message}");
        }

        return true;
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
            // FileLoggerProvider already registered as Singleton above
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

