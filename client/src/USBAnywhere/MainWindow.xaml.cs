// ═══════════════════════════════════════════════════════════════════════════════
// USBAnywhere - USB/IP Client for Windows
// Copyright (c) 2025-2026 SnakeFoxu (OscarLosu) - https://github.com/oscarlosu-lab/USBAnywhere
// Licensed under GPL v3 - See LICENSE file
// ═══════════════════════════════════════════════════════════════════════════════

using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using USBAnywhere.Models;
using USBAnywhere.Services;
using USBAnywhere.Services.Interfaces;
using Forms = System.Windows.Forms;

namespace USBAnywhere;

/// <summary>
/// MainWindow - USB/IP Manager
/// Created by SnakeFoxu (OscarLosu) (OscarLosu) (OscarLosu) - github.com/oscarlosu-lab
/// </summary>
public partial class MainWindow : Window
{
    // Services — injected via DI constructor
    private readonly ILocalizationService _localization;
    private readonly IConfigService _config;
    private readonly INetworkScanner _networkScanner;
    private readonly IUsbipService _usbip;
    private readonly IDriverService _drivers;
    private readonly IFavoritesService _favorites;
    private readonly IUpdateService _updates;
    private readonly IVpnService _vpn;
    private readonly IUsbIdsService _usbIds;
    private readonly IConnectionMonitorService _connectionMonitor;
    private readonly INotificationService _notificationService;

    // TrayIcon
    private Forms.NotifyIcon? _trayIcon;

    // State
    private string _currentLanguage = "es";
    private string _currentTheme = "dark";
    private string _currentReceiveMode = "low-latency";
    private record LogEntry(DateTime Timestamp, string Type, string? Key, object[] Args, string? RawMessage);
    private record SuspendedDeviceState(string ServerIP, string BusId, string Description, string VendorId, string ProductId, string ReceiveMode);
    private readonly List<LogEntry> _logEntries = new();
    private readonly List<SuspendedDeviceState> _suspendedDevices = new();
    private Forms.ToolStripMenuItem? _trayScanItem;
    private Forms.ToolStripMenuItem? _trayDisconnectAllItem;
    private Forms.ToolStripMenuItem? _trayStartMinimizedItem;
    private Forms.ToolStripMenuItem? _trayAutoDetachOnSleepItem;
    private Forms.ToolStripMenuItem? _trayShowItem;
    private Forms.ToolStripMenuItem? _trayExitItem;
    private bool _autoScanStarted;
    private bool _isFirstStartup = true;
    private CancellationTokenSource? _vhciListenerCts;
    private long _vhciRefreshVersion;
    private readonly List<string> _availableSubnets = new();
    private readonly SemaphoreSlim _disconnectAllLock = new(1, 1);
    private readonly StreamingSessionMonitor _streamMonitor = new();
    private readonly SessionIsolationService _sessionIsolation = new();
    private readonly HashSet<int> _streamAttachedPorts = new();
    private System.Windows.Threading.DispatcherTimer? _streamPollTimer;
    private LocalControlApiService? _localApi;

    // IPC Events & Global Win32 Hotkey (Ctrl+Alt+D / CLI --disconnect-all)
    private EventWaitHandle? _disconnectAllEvent;
    private EventWaitHandle? _gracefulExitEvent;
    private RegisteredWaitHandle? _disconnectAllWaitHandle;
    private RegisteredWaitHandle? _gracefulExitWaitHandle;
    private CancellationTokenSource? _ipcPipeCts;
    private HwndSource? _hwndSource;
    private bool _hotkeyRegistered;
    private const int HOTKEY_ID_DISCONNECT_ALL = 0x534B;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public MainWindow(
        ILocalizationService localization,
        IConfigService config,
        INetworkScanner networkScanner,
        IUsbipService usbip,
        IDriverService drivers,
        IFavoritesService favorites,
        IUpdateService updates,
        IVpnService vpn,
        IUsbIdsService usbIds,
        IConnectionMonitorService connectionMonitor,
        INotificationService? notificationService = null)
    {
        _localization = localization;
        _config = config;
        _networkScanner = networkScanner;
        _usbip = usbip;
        _drivers = drivers;
        _favorites = favorites;
        _updates = updates;
        _vpn = vpn;
        _usbIds = usbIds;
        _connectionMonitor = connectionMonitor;
        _notificationService = notificationService ?? new NotificationService();

        InitializeComponent();

        // Subscribe to connection events
        _connectionMonitor.ConnectionLost += OnConnectionLost;
        _connectionMonitor.ConnectionRestored += OnConnectionRestored;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnSystemPowerModeChanged;

        // Load config and apply language + theme
        LoadConfiguration();
        ApplyTheme(_currentTheme);
        UpdateUITexts();

        // Check driver status asynchronously
        CheckDriverStatus();

        // Initialize system tray icon
        InitializeTrayIcon();

        // Register TreeView double-click event for connect/disconnect
        treeDevices.MouseDoubleClick += TreeDevices_MouseDoubleClick;

        // Log startup
        AddLocalizedLog("Info", "log_app_start");

        // Load favorites on startup
        LoadFavorites();

        // Start local HTTP control API if enabled
        InitializeLocalControlApi();

        // Initialize IPC events and global Win32 hotkey (works even when started minimized)
        InitializeIpcAndHotkey();

        // Keyboard fallback when window has focus (Ctrl+Shift+D / Ctrl+Alt+D)
        PreviewKeyDown += MainWindow_PreviewKeyDown;

        // Auto-scan network on startup
        Loaded += async (s, e) => await EnsureStartedAsync();
    }

    /// <summary>
    /// Ensure right-clicking a TreeViewItem selects it before opening ContextMenu.
    /// </summary>
    private void TreeDevices_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source != null && source is not TreeViewItem)
        {
            source = VisualTreeHelper.GetParent(source);
        }

        if (source is TreeViewItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    /// <summary>
    /// Initialize system tray icon with context menu
    /// </summary>
    private void InitializeTrayIcon()
    {
        try
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Text = "USBAnywhere - Virtual USB over IP",
                Visible = true
            };

            Icon? loadedIcon = null;

            // 1. Try embedded WPF pack resource first (works in single-file publish & installer)
            try
            {
                var resInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icon.ico"));
                if (resInfo?.Stream != null)
                {
                    using var s = resInfo.Stream;
                    loadedIcon = new Icon(s, Forms.SystemInformation.SmallIconSize);
                }
            }
            catch (Exception ignoreEx) { System.Diagnostics.Debug.WriteLine($"[MainWindow.xaml.cs] {ignoreEx.Message}"); }

            // 2. Fallback to Assets/icon.ico on disk
            if (loadedIcon == null)
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    using var iconStream = new System.IO.FileStream(iconPath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                    loadedIcon = new Icon(iconStream, Forms.SystemInformation.SmallIconSize);
                }
            }

            // 3. Fallback to executable's associated Win32 icon
            if (loadedIcon == null && !string.IsNullOrEmpty(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
            {
                try { loadedIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch (Exception ignoreEx) { System.Diagnostics.Debug.WriteLine($"[MainWindow.xaml.cs] {ignoreEx.Message}"); }
            }

            _trayIcon.Icon = loadedIcon ?? System.Drawing.SystemIcons.Application;

            // Create context menu
            var trayMenu = new Forms.ContextMenuStrip();

            _trayScanItem = new Forms.ToolStripMenuItem(_localization.GetText("tray_scan", _currentLanguage));
            _trayScanItem.Click += (s, e) => Dispatcher.Invoke(() => BtnScan_Click(this, new RoutedEventArgs()));
            trayMenu.Items.Add(_trayScanItem);

            _trayDisconnectAllItem = new Forms.ToolStripMenuItem(_localization.GetText("tray_disconnect_all", _currentLanguage));
            _trayDisconnectAllItem.Click += async (s, e) => await Dispatcher.InvokeAsync(() => DisconnectAllDevicesFromHotkeyOrCliAsync("Tray"));
            trayMenu.Items.Add(_trayDisconnectAllItem);

            trayMenu.Items.Add(new Forms.ToolStripSeparator());

            _trayStartMinimizedItem = new Forms.ToolStripMenuItem(_localization.GetText("tray_start_minimized", _currentLanguage))
            {
                CheckOnClick = true,
                Checked = _config.Load().StartMinimized
            };
            _trayStartMinimizedItem.Click += (s, e) =>
            {
                var cfg = _config.Load();
                cfg.StartMinimized = _trayStartMinimizedItem.Checked;
                _config.Save(cfg);
                Dispatcher.Invoke(() => chkStartMinimized.IsChecked = cfg.StartMinimized);
            };
            trayMenu.Items.Add(_trayStartMinimizedItem);

            _trayAutoDetachOnSleepItem = new Forms.ToolStripMenuItem(_localization.GetText("tray_auto_detach_sleep", _currentLanguage))
            {
                CheckOnClick = true,
                Checked = _config.Load().AutoDetachOnSleep
            };
            _trayAutoDetachOnSleepItem.Click += (s, e) =>
            {
                var cfg = _config.Load();
                cfg.AutoDetachOnSleep = _trayAutoDetachOnSleepItem.Checked;
                _config.Save(cfg);
                Dispatcher.Invoke(() => chkAutoDetachOnSleep.IsChecked = cfg.AutoDetachOnSleep);
            };
            trayMenu.Items.Add(_trayAutoDetachOnSleepItem);

            trayMenu.Items.Add(new Forms.ToolStripSeparator());

            _trayShowItem = new Forms.ToolStripMenuItem(_localization.GetText("tray_show", _currentLanguage));
            _trayShowItem.Click += (s, e) => Dispatcher.Invoke(() => ShowWindow());
            trayMenu.Items.Add(_trayShowItem);

            trayMenu.Items.Add(new Forms.ToolStripSeparator());

            _trayExitItem = new Forms.ToolStripMenuItem(_localization.GetText("tray_exit", _currentLanguage));
            _trayExitItem.Click += async (s, e) => await Dispatcher.InvokeAsync(GracefulShutdownAsync);
            trayMenu.Items.Add(_trayExitItem);

            _trayIcon.ContextMenuStrip = trayMenu;

            if (_notificationService is NotificationService ns)
            {
                ns.SetFallbackIcon(_trayIcon);
            }

            // Double-click to show window
            _trayIcon.DoubleClick += (s, e) => Dispatcher.Invoke(() => ShowWindow());
        }
        catch (Exception ex)
        {
            AddLocalizedLog("Warning", "log_tray_init_error", ex.Message);
        }
    }

    /// <summary>
    /// Show the main window from tray
    /// </summary>
    private void ShowWindow()
    {
        _isFirstStartup = false;
        Show();
        WindowState = WindowState.Normal;
        Activate();

        if (nodeHubs.Items.Count == 0)
        {
            if (!string.IsNullOrEmpty(txtServerIP.Text))
            {
                Dispatcher.InvokeAsync(() => BtnList_Click(this, new RoutedEventArgs()));
            }
            else
            {
                Dispatcher.InvokeAsync(() => BtnScan_Click(this, new RoutedEventArgs()));
            }
        }
    }

    /// <summary>
    /// Override minimize to minimize to tray instead
    /// </summary>
    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            var config = _config.Load();
            if (config.MinimizeToTray)
            {
                Hide();
                if (!_isFirstStartup)
                {
                    ShowNotification(_localization.GetText("title_main", _currentLanguage),
                                   _localization.GetText("tray_minimized", _currentLanguage),
                                   Forms.ToolTipIcon.Info);
                }
                _isFirstStartup = false;
            }
        }
        base.OnStateChanged(e);
    }

    /// <summary>
    /// Show notification - uses native Windows Toast via NotificationService with fallback to BalloonTip/Popup
    /// </summary>
    private void ShowNotification(string title, string message, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info)
    {
        var notifType = icon switch
        {
            Forms.ToolTipIcon.Error => NotificationType.Error,
            Forms.ToolTipIcon.Warning => NotificationType.Warning,
            _ => NotificationType.Info
        };

        _notificationService.ShowNotification(title, message, notifType);

        if (WindowState == WindowState.Minimized || !IsVisible)
        {
            return;
        }

        toastTitle.Text = icon switch
        {
            Forms.ToolTipIcon.Error => $"❌ {title}",
            Forms.ToolTipIcon.Warning => $"⚠️ {title}",
            _ => $"✅ {title}"
        };
        toastMessage.Text = message;

        toastPopup.Visibility = Visibility.Visible;
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
        toastPopup.BeginAnimation(OpacityProperty, fadeIn);

        var timer = new System.Timers.Timer(1500) { AutoReset = false };
        timer.Elapsed += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (s2, e2) => toastPopup.Visibility = Visibility.Collapsed;
                toastPopup.BeginAnimation(OpacityProperty, fadeOut);
            });
            timer.Dispose();
        };
        timer.Start();
    }

    /// <summary>
    /// Load favorites from config on startup
    /// </summary>
    private void LoadFavorites()
    {
        var favorites = _favorites.GetFavorites();
        nodeFavorites.Items.Clear();
        foreach (var fav in favorites)
        {
            var favNode = new TreeViewItem
            {
                Header = $"⭐ {fav.Description} ({fav.ServerIP}/{fav.BusId})",
                Tag = fav
            };
            nodeFavorites.Items.Add(favNode);
        }
        nodeFavorites.Header = $"⭐ {_localization.GetText("node_favorites", _currentLanguage).Replace("⭐ ", "")} ({favorites.Count})";
    }

    /// <summary>
    /// Auto-scan network on startup
    /// </summary>
    public async Task EnsureStartedAsync()
    {
        if (_autoScanStarted) return;
        _autoScanStarted = true;
        StartVhciDeviceStateListener();
        var config = _config.Load();
        if (config.AutoScanOnStart)
        {
            await AutoScanOnStartup();
        }
        else
        {
            await LoadConnectedDevicesAsync();
            if (config.AutoConnectFavorites)
            {
                await AutoConnectFavorites();
            }
            await HandleStartupCliConnectAsync();
        }
    }

    private async Task AutoScanOnStartup()
    {
        AddLocalizedLog("Info", "log_scan_start");
        lblStatus.Text = _localization.GetText("status_scanning", _currentLanguage);
        lblStatus.Foreground = (SolidColorBrush)FindResource("StatusInfo");

        try
        {
            var subnet = GetLocalSubnet();
            AddLocalizedLog("Info", "log_scanning_subnet", subnet);

            var servers = await _networkScanner.ScanNetworkAsync(subnet);

            AddLocalizedLog("Info", "log_scan_complete", servers.Count);
            foreach (var srv in servers)
            {
                AddLocalizedLog("Info", "log_server_found", srv);
            }

            if (servers.Count > 0)
            {
                nodeHubs.Items.Clear();
                foreach (var server in servers)
                {
                    var serverNode = new TreeViewItem { Header = $"🖥️ {server}" };
                    nodeHubs.Items.Add(serverNode);
                }
                nodeHubs.Header = $"{_localization.GetText("node_hubs", _currentLanguage)} ({servers.Count})";

                txtServerIP.Text = servers[0];

                AddLog("Success", $"{servers.Count} {_localization.GetText("log_scan_found", _currentLanguage)}");
                lblStatus.Text = $"✓ {servers.Count} {_localization.GetText("status_servers_found", _currentLanguage)}";
                lblStatus.Foreground = (SolidColorBrush)FindResource("StatusSuccess");

                foreach (var server in servers)
                {
                    await ListDevicesOnServer(server);
                }

                await LoadConnectedDevicesAsync();
            }
            else
            {
                lblStatus.Text = _localization.GetText("status_no_server", _currentLanguage);
                lblStatus.Foreground = (SolidColorBrush)FindResource("StatusWarning");
                AddLocalizedLog("Warning", "log_scan_none");
                await LoadConnectedDevicesAsync();
            }
        }
        catch (Exception ex)
        {
            AddLog("Error", ex.Message);
            try { await LoadConnectedDevicesAsync(); } catch (Exception ignoreEx) { System.Diagnostics.Debug.WriteLine($"[MainWindow.xaml.cs] {ignoreEx.Message}"); }
        }

        // Always auto-connect saved favorites using their persisted ServerIP even if subnet scan found 0 servers
        var config = _config.Load();
        if (config.AutoConnectFavorites)
        {
            await AutoConnectFavorites();
        }

        await HandleStartupCliConnectAsync();
    }

    private async Task HandleStartupCliConnectAsync()
    {
        try
        {
            if (App.TryParseConnectArgs(Environment.GetCommandLineArgs(), out string cliIp, out string cliBusId))
            {
                await ConnectCliDeviceAsync(cliIp, cliBusId);
            }
        }
        catch (Exception ex)
        {
            AddLog("Error", ex.Message);
        }
    }

    private async Task ConnectCliDeviceAsync(string cliIp, string cliBusId)
    {
        var fav = _favorites.GetFavorites().FirstOrDefault(f =>
            string.Equals(f.ServerIP, cliIp, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.BusId, cliBusId, StringComparison.OrdinalIgnoreCase));
        var requestedMode = _config.Load().DefaultReceiveMode;
        var mode = UsbipService.ResolveEffectiveReceiveMode(requestedMode, fav?.Description, null);
        string? customSerial = GetCustomSerialForDevice(null, cliIp, cliBusId);
        await _usbip.AttachDeviceAsync(cliIp, cliBusId, mode, serial: customSerial);
        await LoadConnectedDevicesAsync();
    }

    /// <summary>
    /// List devices on a specific server and add to tree
    /// </summary>
    private async Task ListDevicesOnServer(string serverIP)
    {
        try
        {
            var devices = await _usbip.ListDevicesAsync(serverIP);

            TreeViewItem? serverNode = null;
            foreach (TreeViewItem item in nodeHubs.Items)
            {
                if (item.Header?.ToString()?.Contains(serverIP, StringComparison.OrdinalIgnoreCase) == true)
                {
                    serverNode = item;
                    break;
                }
            }

            if (serverNode == null)
            {
                serverNode = new TreeViewItem { Header = $"🖥️ {serverIP}" };
                nodeHubs.Items.Add(serverNode);
            }

            serverNode.Items.Clear();
            foreach (var device in devices)
            {
                string displayName = device.Description;

                if (!string.IsNullOrEmpty(device.VendorId) && !string.IsNullOrEmpty(device.ProductId))
                {
                    var customName = _usbIds.GetCustomName(device.VendorId, device.ProductId);
                    if (!string.IsNullOrWhiteSpace(customName))
                    {
                        displayName = customName;
                        device.Description = displayName;
                    }
                    else if (string.IsNullOrWhiteSpace(displayName) ||
                             displayName.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
                             displayName.StartsWith("USB Device", StringComparison.OrdinalIgnoreCase))
                    {
                        var usbIdsName = _usbIds.GetDeviceDescription(device.VendorId, device.ProductId);
                        if (!usbIdsName.StartsWith("Unknown") && !usbIdsName.StartsWith("USB Device"))
                        {
                            displayName = usbIdsName;
                            device.Description = displayName;
                        }
                    }
                }

                bool isConnected = IsDeviceInConnectedNode(serverIP, device.BusId);
                var icon = isConnected ? "☑️" : "🔌";

                var deviceNode = new TreeViewItem
                {
                    Header = $"{icon} {device.BusId}: {displayName}",
                    Tag = device
                };
                serverNode.Items.Add(deviceNode);
            }

            var latencyMs = await MeasureServerLatencyMsAsync(serverIP);
            serverNode.Header = latencyMs >= 0 ? $"🖥️ {serverIP} ({devices.Count})  •  ⚡ {latencyMs:F1} ms" : $"🖥️ {serverIP} ({devices.Count})";
            serverNode.IsExpanded = true;
            nodeHubs.Header = $"{_localization.GetText("node_hubs", _currentLanguage)} ({nodeHubs.Items.Count})";

            AddLocalizedLog("Info", "log_devices_on_server", devices.Count, serverIP);
        }
        catch (Exception ex)
        {
            AddLog("Error", ex.Message);
        }
    }

    private static async Task<double> MeasureServerLatencyMsAsync(string serverIP)
    {
        foreach (var port in new[] { 3240, 3241 })
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(450);
                await client.ConnectAsync(serverIP, port, cts.Token);
                sw.Stop();
                return sw.Elapsed.TotalMilliseconds;
            }
            catch
            {
                // Try fallback port
            }
        }
        return -1;
    }

    private bool IsDeviceInConnectedNode(string serverIP, string busId)
    {
        foreach (TreeViewItem item in nodeConnected.Items)
        {
            if (item.Tag is ConnectedDevice cd &&
                string.Equals(cd.ServerIP, serverIP, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(cd.BusId, busId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private void AddDeviceToConnectedTree(ConnectedDevice device)
    {
        if (IsDeviceInConnectedNode(device.ServerIP, device.BusId))
            return;

        var deviceName = string.IsNullOrEmpty(device.Description)
            ? $"{device.ServerIP}/{device.BusId}"
            : (!string.IsNullOrEmpty(device.VendorId) && !string.IsNullOrEmpty(device.ProductId)
                ? $"{device.BusId}: {device.Description} ({device.VendorId}:{device.ProductId})"
                : $"{device.BusId}: {device.Description}");

        var connNode = new TreeViewItem
        {
            Header = $"☑️ {deviceName}",
            Tag = device
        };
        nodeConnected.Items.Add(connNode);
        UpdateConnectedCount();

        // Also update the icon in nodeHubs if present
        foreach (TreeViewItem hubNode in nodeHubs.Items)
        {
            foreach (TreeViewItem deviceNode in hubNode.Items)
            {
                if (deviceNode.Tag is UsbDevice dev &&
                    string.Equals(dev.ServerIP, device.ServerIP, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(dev.BusId, device.BusId, StringComparison.OrdinalIgnoreCase))
                {
                    deviceNode.Header = $"☑️ {dev.BusId}: {dev.Description}";
                    break;
                }
            }
        }
    }

    private async Task LoadConnectedDevicesAsync()
    {
        try
        {
            var connectedDevices = await _usbip.GetConnectedDevicesAsync();

            if (connectedDevices.Count > 0)
            {
                AddLocalizedLog("Info", "log_devices_already_connected", connectedDevices.Count);

                foreach (var device in connectedDevices)
                {
                    AddDeviceToConnectedTree(device);
                    _connectionMonitor.AddDevice(device.ServerIP, device.BusId, autoReconnect: true);
                }

                _connectionMonitor.Start();
            }
        }
        catch (Exception ex)
        {
            AddLocalizedLog("Warning", "log_load_connected_error", ex.Message);
        }
    }

    private bool HasServerNode(string serverIP)
    {
        foreach (TreeViewItem item in nodeHubs.Items)
        {
            if (item.Header?.ToString()?.Contains(serverIP, StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Auto-connect favorites with AutoConnect=true, resolving effective receive mode
    /// and registering offline favorites in ConnectionMonitorService for automatic plug-in attach.
    /// </summary>
    private async Task AutoConnectFavorites()
    {
        var favorites = _favorites.GetFavorites().Where(f => f.AutoConnect).ToList();
        if (favorites.Count == 0) return;

        AddLocalizedLog("Info", "log_auto_connecting", favorites.Count);

        foreach (var fav in favorites)
        {
            var rawMode = !string.IsNullOrWhiteSpace(fav.ReceiveMode) ? fav.ReceiveMode : _currentReceiveMode;
            var effectiveMode = UsbipService.ResolveEffectiveReceiveMode(rawMode, fav.Description, null);

            if (IsDeviceInConnectedNode(fav.ServerIP, fav.BusId))
            {
                _connectionMonitor.AddDevice(fav.ServerIP, fav.BusId, autoReconnect: true, receiveMode: effectiveMode, initiallyConnected: true);
                _connectionMonitor.Start();
                continue;
            }

            try
            {
                string? favSerial = !string.IsNullOrWhiteSpace(fav.CustomSerial) ? fav.CustomSerial : GetCustomSerialForDevice(null, fav.ServerIP, fav.BusId);
                var result = await _usbip.ConnectAsync(fav.ServerIP, fav.BusId, effectiveMode, serial: favSerial);
                string activeIp = fav.ServerIP;
                string activeBusId = fav.BusId;

                if (!result.Success)
                {
                    var (resolvedIp, resolvedBusId, resolvedChanged) = await _favorites.ResolveFavoriteTargetAsync(fav);
                    if (resolvedChanged)
                    {
                        activeIp = resolvedIp;
                        activeBusId = resolvedBusId;
                        favSerial = !string.IsNullOrWhiteSpace(fav.CustomSerial) ? fav.CustomSerial : GetCustomSerialForDevice(null, activeIp, activeBusId);
                        result = await _usbip.ConnectAsync(activeIp, activeBusId, effectiveMode, serial: favSerial);
                    }
                }

                if (result.Success)
                {
                    AddLocalizedLog("Success", "log_connected_favorite", fav.Description, activeBusId);
                    AddDeviceToConnectedTree(new ConnectedDevice
                    {
                        ServerIP = activeIp,
                        BusId = activeBusId,
                        Description = fav.Description
                    });

                    if (!HasServerNode(activeIp))
                    {
                        if (string.IsNullOrWhiteSpace(txtServerIP.Text))
                            txtServerIP.Text = activeIp;
                        await ListDevicesOnServer(activeIp);
                    }

                    _connectionMonitor.AddDevice(activeIp, activeBusId, autoReconnect: true, receiveMode: effectiveMode, initiallyConnected: true);
                    _connectionMonitor.Start();
                }
                else
                {
                    AddLocalizedLog("Warning", "log_connection_failed", activeBusId, result.Error ?? "");
                    _connectionMonitor.AddDevice(activeIp, activeBusId, autoReconnect: true, receiveMode: effectiveMode, initiallyConnected: false);
                    _connectionMonitor.Start();
                }
            }
            catch (Exception ex)
            {
                AddLocalizedLog("Error", "log_connection_error", fav.BusId, ex.Message);
                _connectionMonitor.AddDevice(fav.ServerIP, fav.BusId, autoReconnect: true, receiveMode: effectiveMode, initiallyConnected: false);
                _connectionMonitor.Start();
            }
        }
    }

    private async Task ConnectDeviceCoreAsync(string serverIP, string busId, string description, string vendorId = "", string productId = "", string? receiveMode = null)
    {
        var requested = !string.IsNullOrWhiteSpace(receiveMode) ? receiveMode : _currentReceiveMode;
        var hwId = (!string.IsNullOrWhiteSpace(vendorId) && !string.IsNullOrWhiteSpace(productId))
            ? $"{vendorId}:{productId}"
            : null;
        var mode = UsbipService.ResolveEffectiveReceiveMode(requested, description, hwId);
        AddLocalizedLog("Info", "log_connecting_device", busId, serverIP);
        lblStatus.Text = $"{_localization.GetText("status_connecting", _currentLanguage)} {busId}...";
        lblStatus.Foreground = (SolidColorBrush)FindResource("StatusInfo");

        string? customSerial = GetCustomSerialForDevice(hwId, serverIP, busId);
        var result = await _usbip.ConnectAsync(serverIP, busId, mode, serial: customSerial);

        if (result.Success)
        {
            AddLocalizedLog("Success", "log_connection_success", busId);
            lblStatus.Text = $"✓ {_localization.GetText("log_connected", _currentLanguage)}: {busId}";
            lblStatus.Foreground = (SolidColorBrush)FindResource("StatusSuccess");

            AddDeviceToConnectedTree(new ConnectedDevice
            {
                ServerIP = serverIP,
                BusId = busId,
                Description = description,
                VendorId = vendorId,
                ProductId = productId
            });

            _connectionMonitor.AddDevice(serverIP, busId, autoReconnect: true, receiveMode: mode);
            _connectionMonitor.Start();

            ShowNotification(
                _localization.GetText("notify_connected", _currentLanguage),
                $"{busId}: {description}",
                Forms.ToolTipIcon.Info);
        }
        else
        {
            AddLog("Error", result.Error ?? _localization.GetText("log_connection_general_failed", _currentLanguage));
            lblStatus.Text = $"Error: {result.Error}";
            lblStatus.Foreground = (SolidColorBrush)FindResource("StatusError");
        }
    }

    private async Task DisconnectDeviceCoreAsync(ConnectedDevice connectedDevice, TreeViewItem selectedNode)
    {
        AddLocalizedLog("Info", "log_disconnecting", connectedDevice.BusId);
        lblStatus.Text = $"{_localization.GetText("status_disconnecting", _currentLanguage)} {connectedDevice.BusId}...";
        lblStatus.Foreground = (SolidColorBrush)FindResource("StatusInfo");

        var connectedDevices = await _usbip.GetConnectedDevicesAsync();

        // 1. Match exact ServerIP AND BusId first so multi-device servers disconnect the right port
        var portDevice = connectedDevices.FirstOrDefault(d =>
            string.Equals(d.ServerIP?.Trim(), connectedDevice.ServerIP?.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(d.BusId) &&
            string.Equals(d.BusId, connectedDevice.BusId, StringComparison.OrdinalIgnoreCase));

        // 2. Safe fallback: if exact IP didn't match (e.g. hostname vs IP resolution difference),
        // match BusId and ensure there is exactly one unique candidate before disconnecting
        if (portDevice == null)
        {
            var candidates = connectedDevices.Where(d =>
                !string.IsNullOrEmpty(d.BusId) &&
                string.Equals(d.BusId, connectedDevice.BusId, StringComparison.OrdinalIgnoreCase) &&
                (d.ServerIP.Contains(connectedDevice.ServerIP, StringComparison.OrdinalIgnoreCase) ||
                 connectedDevice.ServerIP.Contains(d.ServerIP, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            if (candidates.Count == 1)
            {
                portDevice = candidates[0];
            }
            else
            {
                // Fallback to exact ServerIP alone if unique
                portDevice = connectedDevices.FirstOrDefault(d =>
                    string.Equals(d.ServerIP?.Trim(), connectedDevice.ServerIP?.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }

        if (portDevice != null)
        {
            // Remove from monitor before detaching to prevent auto-reconnect race during detach
            _connectionMonitor.RemoveDevice(connectedDevice.ServerIP, connectedDevice.BusId);
            var result = await _usbip.DisconnectAsync(portDevice.Port);

            if (result.Success)
            {
                AddLocalizedLog("Success", "log_disconnected_port", portDevice.Port);
                lblStatus.Text = $"✓ {_localization.GetText("log_disconnected", _currentLanguage)}";
                lblStatus.Foreground = (SolidColorBrush)FindResource("StatusSuccess");
                nodeConnected.Items.Remove(selectedNode);
                UpdateConnectedCount();

                foreach (TreeViewItem hubNode in nodeHubs.Items)
                {
                    foreach (TreeViewItem deviceNode in hubNode.Items)
                    {
                        if (deviceNode.Tag is UsbDevice dev &&
                            string.Equals(dev.ServerIP, connectedDevice.ServerIP, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(dev.BusId, connectedDevice.BusId, StringComparison.OrdinalIgnoreCase))
                        {
                            deviceNode.Header = $"🔌 {dev.BusId}: {dev.Description}";
                            break;
                        }
                    }
                }

                ShowNotification(
                    _localization.GetText("log_disconnected", _currentLanguage),
                    $"{connectedDevice.BusId}",
                    Forms.ToolTipIcon.Info);
            }
            else
            {
                AddLog("Error", result.Error ?? _localization.GetText("log_disconnect_failed", _currentLanguage));
            }
        }
        else
        {
            AddLocalizedLog("Warning", "log_device_not_found");
            nodeConnected.Items.Remove(selectedNode);
            _connectionMonitor.RemoveDevice(connectedDevice.ServerIP, connectedDevice.BusId);
            UpdateConnectedCount();
        }
    }

    /// <summary>
    /// TreeView double-click handler for connect/disconnect
    /// </summary>
    private async void TreeDevices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (treeDevices.SelectedItem is not TreeViewItem selectedNode)
            return;

        if (selectedNode.Tag is UsbDevice device)
        {
            await ConnectDeviceCoreAsync(device.ServerIP, device.BusId, device.Description, device.VendorId, device.ProductId);
        }
        else if (selectedNode.Tag is FavoriteDevice fav)
        {
            await ConnectDeviceCoreAsync(fav.ServerIP, fav.BusId, fav.Description, receiveMode: fav.ReceiveMode);
        }
        else if (selectedNode.Tag is ConnectedDevice connectedDevice)
        {
            await DisconnectDeviceCoreAsync(connectedDevice, selectedNode);
        }
    }

    private void UpdateConnectedCount()
    {
        nodeConnected.Header = $"☑️ {_localization.GetText("node_connected", _currentLanguage).Replace("☑️ ", "")} ({nodeConnected.Items.Count})";
    }

    private bool _isShuttingDown = false;

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isShuttingDown)
        {
            e.Cancel = true;
            await GracefulShutdownAsync();
            return;
        }

        if (_trayIcon != null)
        {
            try
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            catch (Exception ignoreEx) { System.Diagnostics.Debug.WriteLine($"[MainWindow.xaml.cs] {ignoreEx.Message}"); }
            _trayIcon = null;
        }

        _connectionMonitor?.Stop();

        base.OnClosing(e);
    }

    #region Window Controls

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch (Exception ignoreEx) { System.Diagnostics.Debug.WriteLine($"[MainWindow.xaml.cs] {ignoreEx.Message}"); }
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private async void BtnClose_Click(object sender, RoutedEventArgs e) => await GracefulShutdownAsync();

    private void ChkStartMinimized_Click(object sender, RoutedEventArgs e)
    {
        bool isChecked = chkStartMinimized.IsChecked == true;
        var cfg = _config.Load();
        cfg.StartMinimized = isChecked;
        _config.Save(cfg);
        if (_trayStartMinimizedItem != null)
        {
            _trayStartMinimizedItem.Checked = isChecked;
        }
    }

    private void ChkAutoDetachOnSleep_Click(object sender, RoutedEventArgs e)
    {
        bool isChecked = chkAutoDetachOnSleep.IsChecked == true;
        var cfg = _config.Load();
        cfg.AutoDetachOnSleep = isChecked;
        _config.Save(cfg);
        if (_trayAutoDetachOnSleepItem != null)
        {
            _trayAutoDetachOnSleepItem.Checked = isChecked;
        }
    }

    private void BtnPair_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Views.PairServerDialog(_config, txtServerIP.Text) { Owner = this };
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.PairedHost))
        {
            txtServerIP.Text = dlg.PairedHost;
            AddLog("Success", $"Emparejado con {dlg.PairedHost}");
        }
    }

    private void ChkLocalApi_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = chkLocalApi.IsChecked == true;
        var cfg = _config.Load();
        cfg.LocalApiEnabled = enabled;
        if (enabled && string.IsNullOrWhiteSpace(cfg.LocalApiToken))
        {
            cfg.LocalApiToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        }
        _config.Save(cfg);

        if (enabled && _localApi == null)
        {
            try
            {
                _localApi = new LocalControlApiService(_config, _usbip, _drivers, _favorites, _streamMonitor, cfg.LocalApiPort);
                _localApi.Start();
            }
            catch (Exception ex) { AddLog("Warning", $"Local API error: {ex.Message}"); }
        }
        else if (!enabled && _localApi != null)
        {
            try { _localApi.Dispose(); } catch { }
            _localApi = null;
        }
    }

    private void BtnCopyApiToken_Click(object sender, RoutedEventArgs e)
    {
        var cfg = _config.Load();
        if (string.IsNullOrWhiteSpace(cfg.LocalApiToken))
        {
            cfg.LocalApiToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            _config.Save(cfg);
        }
        System.Windows.Clipboard.SetText(cfg.LocalApiToken);
        AddLog("Success", "Token Bearer de API Local copiado al portapapeles");
    }

    private void ChkSessionIsolation_Click(object sender, RoutedEventArgs e)
    {
        var cfg = _config.Load();
        cfg.EnforceSessionIsolation = chkSessionIsolation.IsChecked == true;
        _config.Save(cfg);
    }

    private void MenuSetAttachMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || mi.Tag is not string mode) return;
        if (treeDevices.SelectedItem is TreeViewItem node)
        {
            FavoriteDevice? fav = node.Tag as FavoriteDevice;
            if (fav == null && node.Tag is UsbDevice dev)
            {
                fav = _favorites.GetFavorites().FirstOrDefault(f =>
                    string.Equals(f.ServerIP, dev.ServerIP, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.BusId, dev.BusId, StringComparison.OrdinalIgnoreCase));
            }
            if (fav != null)
            {
                fav.AttachMode = mode;
                var cfg = _config.Load();
                var target = cfg.Favorites.FirstOrDefault(f =>
                    string.Equals(f.ServerIP, fav.ServerIP, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.BusId, fav.BusId, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    target.AttachMode = mode;
                    _config.Save(cfg);
                }
                AddLog("Info", $"Modo de conexión para {fav.Description} -> {mode}");
            }
        }
    }

    private async Task GracefulShutdownAsync()
    {
        _isShuttingDown = true;
        try
        {
            CleanupIpcAndHotkey();
            _connectionMonitor.Stop();
            var config = _config.Load();
            config.Language = _currentLanguage;
            config.Theme = _currentTheme;
            config.DefaultReceiveMode = _currentReceiveMode;
            config.LastServerIP = txtServerIP.Text;
            config.Favorites = _favorites.GetFavorites().ToList();
            _config.Save(config);
            if (config.AutoDetachOnExit)
            {
                AddLocalizedLog("Info", "log_detaching_on_exit");
                await _usbip.DetachAllConnectedDevicesAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Shutdown] Error al desconectar dispositivos: {ex.Message}");
        }
        finally
        {
            _trayIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
    }

    #endregion

    #region Language, Theme & Receive Mode

    private void BtnLanguage_Click(object sender, RoutedEventArgs e)
    {
        _currentLanguage = _currentLanguage == "es" ? "en" : "es";
        _config.SetLanguage(_currentLanguage);
        UpdateUITexts();
        CheckDriverStatus();
    }

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        _currentTheme = _currentTheme == "dark" ? "light" : "dark";
        ApplyTheme(_currentTheme);
        var config = _config.Load();
        config.Theme = _currentTheme;
        _config.Save(config);
    }

    private void BtnReceiveMode_Click(object sender, RoutedEventArgs e)
    {
        _currentReceiveMode = string.Equals(_currentReceiveMode, "low-latency", StringComparison.OrdinalIgnoreCase)
            ? "zero-copy"
            : "low-latency";

        UpdateReceiveModeButton();

        var config = _config.Load();
        config.DefaultReceiveMode = _currentReceiveMode;
        _config.Save(config);

        AddLocalizedLog("Info", _currentReceiveMode == "low-latency" ? "log_mode_lowlatency" : "log_mode_zerocopy");
    }

    private void UpdateReceiveModeButton()
    {
        bool isLowLatency = string.Equals(_currentReceiveMode, "low-latency", StringComparison.OrdinalIgnoreCase);
        btnReceiveMode.Content = isLowLatency
            ? _localization.GetText("mode_lowlatency", _currentLanguage)
            : _localization.GetText("mode_zerocopy", _currentLanguage);
        btnReceiveMode.Background = new SolidColorBrush(
            isLowLatency
                ? System.Windows.Media.Color.FromRgb(211, 84, 0)
                : System.Windows.Media.Color.FromRgb(16, 124, 65));
    }

    private static SolidColorBrush BrushFromHex(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

    private void ApplyTheme(string theme)
    {
        btnTheme.Content = theme == "dark" ? "🌙" : "☀️";
        var resources = System.Windows.Application.Current.Resources;

        if (theme == "light")
        {
            // Obsidian Precision Light Theme (v1.0.0)
            resources["BackgroundDark"] = BrushFromHex("#F4F4F6");
            resources["BackgroundPanel"] = BrushFromHex("#FFFFFF");
            resources["BackgroundTitleBar"] = BrushFromHex("#EAEAEF");
            resources["BackgroundButton"] = BrushFromHex("#E5E7EB");
            resources["BackgroundButtonHover"] = BrushFromHex("#D1D5DB");
            resources["BackgroundInput"] = BrushFromHex("#FFFFFF");
            resources["ForegroundPrimary"] = BrushFromHex("#111827");
            resources["ForegroundSecondary"] = BrushFromHex("#4B5563");
            resources["ForegroundAccent"] = BrushFromHex("#111827");
            resources["AccentButtonForeground"] = BrushFromHex("#FFFFFF");
            resources["BorderSubtle"] = BrushFromHex("#D1D5DB");
            resources["AccentOrange"] = BrushFromHex("#111827");
            resources["AccentBlue"] = BrushFromHex("#111827");
            resources["AccentGreen"] = BrushFromHex("#15803D");
            resources["AccentRed"] = BrushFromHex("#DC2626");
            resources["AccentTeal"] = BrushFromHex("#0284C7");
            resources["StatusSuccess"] = BrushFromHex("#15803D");
            resources["StatusWarning"] = BrushFromHex("#B45309");
            resources["StatusError"] = BrushFromHex("#DC2626");
            resources["StatusInfo"] = BrushFromHex("#0284C7");
        }
        else
        {
            // Obsidian Precision Dark Theme (v1.0.0)
            resources["BackgroundDark"] = BrushFromHex("#0B0C0E");
            resources["BackgroundPanel"] = BrushFromHex("#131417");
            resources["BackgroundTitleBar"] = BrushFromHex("#16171B");
            resources["BackgroundButton"] = BrushFromHex("#18191E");
            resources["BackgroundButtonHover"] = BrushFromHex("#22242A");
            resources["BackgroundInput"] = BrushFromHex("#141518");
            resources["ForegroundPrimary"] = BrushFromHex("#EDEDED");
            resources["ForegroundSecondary"] = BrushFromHex("#8E919A");
            resources["ForegroundAccent"] = BrushFromHex("#D2F800");
            resources["AccentButtonForeground"] = BrushFromHex("#0B0C0E");
            resources["BorderSubtle"] = BrushFromHex("#22242A");
            resources["AccentOrange"] = BrushFromHex("#D2F800");
            resources["AccentBlue"] = BrushFromHex("#D2F800");
            resources["AccentGreen"] = BrushFromHex("#22C55E");
            resources["AccentRed"] = BrushFromHex("#EF4444");
            resources["AccentTeal"] = BrushFromHex("#00E5FF");
            resources["StatusSuccess"] = BrushFromHex("#D2F800");
            resources["StatusWarning"] = BrushFromHex("#F59E0B");
            resources["StatusError"] = BrushFromHex("#EF4444");
            resources["StatusInfo"] = BrushFromHex("#00E5FF");
        }

        Background = (SolidColorBrush)resources["BackgroundDark"];
    }

    private void UpdateUITexts()
    {
        Title = $"{_localization.GetText("title_main", _currentLanguage)} - {_localization.GetText("subtitle_main", _currentLanguage)}";
        if (txtTitleSubtitle != null) txtTitleSubtitle.Text = _localization.GetText("subtitle_main", _currentLanguage);
        btnLanguage.Content = _currentLanguage == "es" ? "🌐 ES" : "🌐 EN";
        btnLanguage.ToolTip = _localization.GetText("tooltip_lang", _currentLanguage);
        btnTheme.ToolTip = _localization.GetText("tooltip_theme", _currentLanguage);
        btnUpdate.ToolTip = _localization.GetText("tooltip_update", _currentLanguage);
        btnMinimize.ToolTip = _localization.GetText("tooltip_minimize", _currentLanguage);
        btnMaximize.ToolTip = _localization.GetText("tooltip_maximize", _currentLanguage);
        btnClose.ToolTip = _localization.GetText("tooltip_close", _currentLanguage);
        btnUpdate.Content = _localization.GetText("btn_update", _currentLanguage);
        lblServer.Content = _localization.GetText("lbl_server", _currentLanguage);
        lblDriverStatus.Text = _localization.GetText("lbl_drivers", _currentLanguage);
        btnScan.Content = _localization.GetText("btn_scan", _currentLanguage);
        btnList.Content = _localization.GetText("btn_refresh", _currentLanguage);
        btnSSH.Content = _localization.GetText("btn_ssh", _currentLanguage);
        btnVPN.Content = _localization.GetText("btn_vpn", _currentLanguage);
        nodeHubs.Header = $"{_localization.GetText("node_hubs", _currentLanguage)} ({nodeHubs.Items.Count})";
        UpdateConnectedCount();
        nodeFavorites.Header = $"⭐ {_localization.GetText("node_favorites", _currentLanguage).Replace("⭐ ", "")} ({nodeFavorites.Items.Count})";
        lblLogTitle.Content = _localization.GetText("log_title", _currentLanguage);
        btnClearLog.Content = _localization.GetText("log_clear", _currentLanguage);
        btnInstallDrivers.Content = _localization.GetText("btn_install_drivers", _currentLanguage);
        btnUninstallDrivers.Content = _localization.GetText("btn_uninstall_drivers", _currentLanguage);
        lblStatus.Text = _localization.GetText("status_ready", _currentLanguage);

        // Localize ContextMenu
        menuConnect.Header = _localization.GetText("menu_connect", _currentLanguage);
        menuDisconnect.Header = _localization.GetText("menu_disconnect", _currentLanguage);
        menuAddFavorite.Header = _localization.GetText("menu_add_favorite", _currentLanguage);
        menuRemoveFavorite.Header = _localization.GetText("menu_remove_favorite", _currentLanguage);
        menuRemoveServer.Header = _localization.GetText("menu_remove_server", _currentLanguage);
        menuProperties.Header = _localization.GetText("menu_properties", _currentLanguage);
        menuRename.Header = _localization.GetText("menu_rename", _currentLanguage);
        menuSetSerial.Header = _localization.GetText("menu_set_serial", _currentLanguage);
        menuToggleKernelPersist.Header = _localization.GetText("menu_kernel_persist", _currentLanguage);
        menuStopAttachRetries.Header = _localization.GetText("menu_stop_attach_retries", _currentLanguage);
        menuConfigureHubPorts.Header = _localization.GetText("menu_hub_ports", _currentLanguage);
        menuExportDiagnostics.Header = _localization.GetText("menu_export_diagnostics", _currentLanguage);

        chkStartMinimized.Content = _localization.GetText("chk_start_minimized", _currentLanguage);
        chkAutoDetachOnSleep.Content = _localization.GetText("chk_auto_detach_sleep", _currentLanguage);

        // Localize Tray Menu
        if (_trayScanItem != null) _trayScanItem.Text = _localization.GetText("tray_scan", _currentLanguage);
        if (_trayDisconnectAllItem != null) _trayDisconnectAllItem.Text = _localization.GetText("tray_disconnect_all", _currentLanguage);
        if (_trayStartMinimizedItem != null) _trayStartMinimizedItem.Text = _localization.GetText("tray_start_minimized", _currentLanguage);
        if (_trayAutoDetachOnSleepItem != null) _trayAutoDetachOnSleepItem.Text = _localization.GetText("tray_auto_detach_sleep", _currentLanguage);
        if (_trayShowItem != null) _trayShowItem.Text = _localization.GetText("tray_show", _currentLanguage);
        if (_trayExitItem != null) _trayExitItem.Text = _localization.GetText("tray_exit", _currentLanguage);

        UpdateReceiveModeButton();
        RenderLogs();
    }

    #endregion

    #region Network Scanning

    private async void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        lblStatus.Text = _localization.GetText("status_scanning", _currentLanguage);
        lblStatus.Foreground = (SolidColorBrush)FindResource("StatusInfo");
        progressBar.Visibility = Visibility.Visible;
        progressBar.IsIndeterminate = true;

        AddLocalizedLog("Info", "log_scan_start");

        try
        {
            var servers = await _networkScanner.ScanNetworkAsync(GetLocalSubnet());

            nodeHubs.Items.Clear();
            foreach (var server in servers)
            {
                var serverNode = new TreeViewItem { Header = $"🖥️ {server}" };
                nodeHubs.Items.Add(serverNode);
            }
            nodeHubs.Header = $"{_localization.GetText("node_hubs", _currentLanguage)} ({servers.Count})";

            if (servers.Count > 0)
            {
                lblStatus.Text = $"✓ {servers.Count} {_localization.GetText("status_servers_found", _currentLanguage)}";
                lblStatus.Foreground = (SolidColorBrush)FindResource("StatusSuccess");
                AddLog("Success", $"{servers.Count} {_localization.GetText("log_scan_found", _currentLanguage)}");

                txtServerIP.Text = servers[0];

                foreach (var server in servers)
                {
                    await ListDevicesOnServer(server);
                }
            }
            else
            {
                lblStatus.Text = _localization.GetText("status_no_server", _currentLanguage);
                lblStatus.Foreground = (SolidColorBrush)FindResource("StatusWarning");
                AddLocalizedLog("Warning", "log_scan_none");
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error: {ex.Message}";
            lblStatus.Foreground = (SolidColorBrush)FindResource("StatusError");
            AddLog("Error", ex.Message);
        }
        finally
        {
            progressBar.Visibility = Visibility.Hidden;
            progressBar.IsIndeterminate = false;
        }
    }

    private string GetLocalSubnet()
    {
        try
        {
            string? fallbackSubnet = null;
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains("WSL", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var props = ni.GetIPProperties();
                bool hasGateway = props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !g.Address.Equals(System.Net.IPAddress.Any));

                foreach (var ip in props.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var addrStr = ip.Address.ToString();
                        if (addrStr.StartsWith("169.254.", StringComparison.Ordinal))
                            continue;

                        var parts = addrStr.Split('.');
                        var subnet = $"{parts[0]}.{parts[1]}.{parts[2]}";
                        if (hasGateway)
                            return subnet;
                        fallbackSubnet ??= subnet;
                    }
                }
            }
            if (fallbackSubnet != null)
                return fallbackSubnet;
        }
        catch (Exception dbgEx) { System.Diagnostics.Debug.WriteLine($"[MainWindow.xaml] {dbgEx.Message}"); }
        return "192.168.1";
    }

    #endregion

    #region USBIP Operations

    private void TxtServerIP_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            BtnList_Click(sender, new RoutedEventArgs());
        }
    }

    private async void BtnList_Click(object sender, RoutedEventArgs e)
    {
        var servers = new List<string>();

        foreach (TreeViewItem item in nodeHubs.Items)
        {
            var header = item.Header?.ToString();
            if (header != null)
            {
                var match = System.Text.RegularExpressions.Regex.Match(header, @"🖥️\s+([a-zA-Z0-9\.\-]+)");
                if (match.Success)
                {
                    var host = match.Groups[1].Value;
                    if (!servers.Contains(host, StringComparer.OrdinalIgnoreCase))
                        servers.Add(host);
                }
            }
        }

        // Also include the IP/host typed in txtServerIP even if other hubs are already in the tree
        var typedIP = txtServerIP.Text.Trim();
        if (!string.IsNullOrEmpty(typedIP) && !servers.Contains(typedIP, StringComparer.OrdinalIgnoreCase))
        {
            servers.Add(typedIP);
        }

        if (servers.Count == 0)
        {
            lblStatus.Text = _localization.GetText("enter_ip", _currentLanguage);
            lblStatus.Foreground = (SolidColorBrush)FindResource("StatusWarning");
            return;
        }

        lblStatus.Text = $"{_localization.GetText("status_listing", _currentLanguage)} {servers.Count} server(s)...";
        lblStatus.Foreground = (SolidColorBrush)FindResource("StatusInfo");

        AddLocalizedLog("Info", "log_listing_devices", servers.Count);

        int totalDevices = 0;

        try
        {
            foreach (var serverIP in servers)
            {
                await ListDevicesOnServer(serverIP);

                foreach (TreeViewItem item in nodeHubs.Items)
                {
                    if (item.Header?.ToString()?.Contains(serverIP, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        totalDevices += item.Items.Count;
                        break;
                    }
                }
            }

            lblStatus.Text = $"✓ {totalDevices} {_localization.GetText("status_devices_found", _currentLanguage)} on {servers.Count} server(s)";
            lblStatus.Foreground = (SolidColorBrush)FindResource("StatusSuccess");
            AddLocalizedLog("Success", "log_listed_devices", totalDevices, servers.Count);
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error: {ex.Message}";
            lblStatus.Foreground = (SolidColorBrush)FindResource("StatusError");
            AddLog("Error", ex.Message);
        }
    }

    #endregion

    #region SSH & VPN

    private void BtnSSH_Click(object sender, RoutedEventArgs e)
    {
        var serverIP = txtServerIP.Text.Trim();
        if (string.IsNullOrEmpty(serverIP))
        {
            foreach (TreeViewItem item in nodeHubs.Items)
            {
                var header = item.Header?.ToString();
                if (header != null)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(header, @"(?:🖥️\s+|🖧\s+)?([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}|[a-zA-Z0-9\.\-]+)");
                    if (match.Success)
                    {
                        serverIP = match.Groups[1].Value;
                        break;
                    }
                }
            }
        }

        var sshDialog = new Views.SshDialog(_localization, _currentLanguage, serverIP)
        {
            Owner = this
        };
        sshDialog.ShowDialog();
    }

    private async void BtnVPN_Click(object sender, RoutedEventArgs e)
    {
        var vpnDialog = new Views.VpnDialog(_localization, _currentLanguage, _vpn)
        {
            Owner = this
        };
        vpnDialog.ShowDialog();

        if (vpnDialog.SelectedPeerIPs.Count > 0)
        {
            txtServerIP.Text = vpnDialog.SelectedPeerIPs[0];
            foreach (var peerIp in vpnDialog.SelectedPeerIPs)
            {
                if (USBAnywhere.Utils.InputValidator.IsValidHost(peerIp))
                {
                    await ListDevicesOnServer(peerIp);
                }
            }
        }
    }

    #endregion

    #region Update

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        lblStatus.Text = _localization.GetText("update_checking", _currentLanguage);
        lblStatus.Foreground = (SolidColorBrush)FindResource("StatusInfo");
        AddLocalizedLog("Info", "log_checking_updates");

        try
        {
            var updateInfo = await _updates.CheckForUpdatesAsync();

            AddLog("Debug", $"Current: v{updateInfo.CurrentVersion} | Latest: v{updateInfo.LatestVersion} | Update: {updateInfo.UpdateAvailable}");

            if (updateInfo.UpdateAvailable)
            {
                AddLocalizedLog("Info", "log_update_available", updateInfo.LatestVersion ?? "");
                lblStatus.Text = $"⬆️ {_localization.GetText("update_available", _currentLanguage)}: v{updateInfo.LatestVersion}";
                lblStatus.Foreground = (SolidColorBrush)FindResource("StatusWarning");

                var prompt = _localization.GetText("msg_update_prompt", _currentLanguage, updateInfo.LatestVersion ?? "", updateInfo.ReleaseNotes ?? "");
                var title = _localization.GetText("msg_update_title", _currentLanguage);
                var result = System.Windows.MessageBox.Show(prompt, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Information);

                if (result == System.Windows.MessageBoxResult.Yes && updateInfo.DownloadUrl != null && updateInfo.InstallerName != null)
                {
                    AddLocalizedLog("Info", "log_downloading_update");
                    lblStatus.Text = _localization.GetText("update_downloading", _currentLanguage);

                    var installResult = await _updates.StartUpdateAsync(updateInfo.DownloadUrl, updateInfo.InstallerName, updateInfo.HashUrl);
                    if (!installResult.Success)
                    {
                        AddLog("Error", installResult.Error ?? _localization.GetText("log_update_failed", _currentLanguage));
                    }
                }
            }
            else if (!string.IsNullOrEmpty(updateInfo.Error))
            {
                lblStatus.Text = $"❌ {_localization.GetText("log_update_failed", _currentLanguage)}: {updateInfo.Error}";
                lblStatus.Foreground = (SolidColorBrush)FindResource("StatusError");
                AddLog("Error", $"Update check failed: {updateInfo.Error}");
            }
            else
            {
                lblStatus.Text = _localization.GetText("update_current", _currentLanguage);
                lblStatus.Foreground = (SolidColorBrush)FindResource("StatusSuccess");
                AddLocalizedLog("Success", "log_latest_version", updateInfo.CurrentVersion ?? "");
            }
        }
        catch (Exception ex)
        {
            AddLog("Error", ex.Message);
            lblStatus.Text = "Error checking updates";
            lblStatus.Foreground = (SolidColorBrush)FindResource("StatusError");
        }
    }

    #endregion

    #region Drivers

    private async void CheckDriverStatus()
    {
        try
        {
            var status = await Task.Run(() => _drivers.GetDriverStatus());
            var installed = string.Equals(status, "INSTALADO", StringComparison.OrdinalIgnoreCase);
            lblDriverValue.Text = installed
                ? _localization.GetText("driver_installed", _currentLanguage)
                : (string.Equals(status, "NO_INSTANCIADO", StringComparison.OrdinalIgnoreCase)
                    ? _localization.GetText("driver_not_instantiated", _currentLanguage)
                    : _localization.GetText("driver_not_installed", _currentLanguage));
            lblDriverValue.Foreground = installed
                ? (SolidColorBrush)FindResource("StatusSuccess")
                : (SolidColorBrush)FindResource("StatusError");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CheckDriverStatus] {ex.Message}");
        }
    }

    private async void BtnInstallDrivers_Click(object sender, RoutedEventArgs e)
    {
        btnInstallDrivers.IsEnabled = false;
        btnUninstallDrivers.IsEnabled = false;
        try
        {
            AddLocalizedLog("Info", "log_installing_drivers");
            var success = await _drivers.InstallDriverAsync();
            if (success)
            {
                AddLocalizedLog("Success", "log_drivers_installed");
                CheckDriverStatus();
            }
            else
            {
                AddLocalizedLog("Error", "log_install_drivers_failed");
            }
        }
        finally
        {
            btnInstallDrivers.IsEnabled = true;
            btnUninstallDrivers.IsEnabled = true;
        }
    }

    private async void BtnUninstallDrivers_Click(object sender, RoutedEventArgs e)
    {
        btnInstallDrivers.IsEnabled = false;
        btnUninstallDrivers.IsEnabled = false;
        try
        {
            AddLocalizedLog("Info", "log_uninstalling_drivers");
            try
            {
                await _usbip.DetachAllConnectedDevicesAsync();
                nodeConnected.Items.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UninstallDrivers] Detach warning: {ex.Message}");
            }

            var success = await _drivers.UninstallDriverAsync();
            if (success)
            {
                AddLocalizedLog("Success", "log_drivers_uninstalled");
                CheckDriverStatus();
            }
            else
            {
                AddLocalizedLog("Error", "log_uninstall_drivers_failed");
            }
        }
        finally
        {
            btnInstallDrivers.IsEnabled = true;
            btnUninstallDrivers.IsEnabled = true;
        }
    }

    #endregion

    #region Log

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        _logEntries.Clear();
        txtLog.Text = "";
    }

    private void AddLocalizedLog(string type, string key, params object[] args)
    {
        var item = new LogEntry(DateTime.Now, type, key, args, null);
        _logEntries.Insert(0, item);
        if (_logEntries.Count > 100)
            _logEntries.RemoveAt(_logEntries.Count - 1);
        RenderLogs();
    }

    private void AddLog(string type, string message)
    {
        var item = new LogEntry(DateTime.Now, type, null, Array.Empty<object>(), message);
        _logEntries.Insert(0, item);
        if (_logEntries.Count > 100)
            _logEntries.RemoveAt(_logEntries.Count - 1);
        RenderLogs();
    }

    private void RenderLogs()
    {
        var lines = new List<string>(_logEntries.Count);
        foreach (var entry in _logEntries)
        {
            var emoji = entry.Type switch
            {
                "Success" => "☑",
                "Warning" => "⚠",
                "Error" => "❌",
                _ => "ℹ"
            };
            string message;
            if (!string.IsNullOrEmpty(entry.Key))
            {
                var template = _localization.GetText(entry.Key, _currentLanguage);
                message = entry.Args != null && entry.Args.Length > 0 ? string.Format(template, entry.Args) : template;
            }
            else
            {
                message = entry.RawMessage ?? "";
            }
            lines.Add($"[{entry.Timestamp:HH:mm:ss}] {emoji} {message}");
        }
        txtLog.Text = string.Join("\n", lines);
    }

    #endregion

    #region Configuration

    private void LoadConfiguration()
    {
        var config = _config.Load();
        _currentLanguage = config.Language ?? "es";
        _currentTheme = config.Theme ?? "dark";
        _currentReceiveMode = string.IsNullOrWhiteSpace(config.DefaultReceiveMode) ? "low-latency" : config.DefaultReceiveMode;
        UpdateReceiveModeButton();

        if (!string.IsNullOrEmpty(config.LastServerIP))
            txtServerIP.Text = config.LastServerIP;

        chkStartMinimized.IsChecked = config.StartMinimized;
        if (_trayStartMinimizedItem != null)
            _trayStartMinimizedItem.Checked = config.StartMinimized;
        chkAutoDetachOnSleep.IsChecked = config.AutoDetachOnSleep;
        if (_trayAutoDetachOnSleepItem != null)
            _trayAutoDetachOnSleepItem.Checked = config.AutoDetachOnSleep;
        chkLocalApi.IsChecked = config.LocalApiEnabled;
        chkSessionIsolation.IsChecked = config.EnforceSessionIsolation;

        _usbIds.LoadCustomNames(config.CustomDeviceNames);
    }

    private async void OnSystemPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        try
        {
            var config = _config.Load();
            if (!config.AutoDetachOnSleep) return;

            if (e.Mode == Microsoft.Win32.PowerModes.Suspend)
            {
                _connectionMonitor.Stop();
                _suspendedDevices.Clear();

                // Release local VHCI ports synchronously (< 5ms) before Windows suspends the process
                var fastDetached = _usbip.DetachAllConnectedDevicesFastForSuspend();
                if (fastDetached.Count > 0)
                {
                    foreach (var dev in fastDetached)
                    {
                        _suspendedDevices.Add(new SuspendedDeviceState(
                            dev.ServerIP,
                            dev.BusId,
                            dev.Description,
                            dev.VendorId,
                            dev.ProductId,
                            _currentReceiveMode));
                    }

                    await Dispatcher.InvokeAsync(() =>
                    {
                        nodeConnected.Items.Clear();
                        UpdateConnectedCount();
                    });

                    await _usbip.NotifyRemoteUnbindBestEffortAsync(
                        fastDetached.Select(d => (d.ServerIP, d.BusId)),
                        timeoutMs: 800);
                    return;
                }

                var connected = await _usbip.GetConnectedDevicesAsync();
                foreach (var dev in connected)
                {
                    _suspendedDevices.Add(new SuspendedDeviceState(
                        dev.ServerIP,
                        dev.BusId,
                        dev.Description,
                        dev.VendorId,
                        dev.ProductId,
                        _currentReceiveMode));
                }

                if (_suspendedDevices.Count > 0)
                {
                    await _usbip.DetachAllConnectedDevicesAsync();
                    await Dispatcher.InvokeAsync(() =>
                    {
                        nodeConnected.Items.Clear();
                        UpdateConnectedCount();
                    });
                }
            }
            else if (e.Mode == Microsoft.Win32.PowerModes.Resume && _suspendedDevices.Count > 0)
            {
                var toRestore = _suspendedDevices.ToList();
                _suspendedDevices.Clear();
                await Task.Delay(2500);
                await Dispatcher.InvokeAsync(async () =>
                {
                    foreach (var dev in toRestore)
                    {
                        await ConnectDeviceCoreAsync(dev.ServerIP, dev.BusId, dev.Description, dev.VendorId, dev.ProductId, dev.ReceiveMode);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PowerModeChanged] {ex.Message}");
        }
    }

    private void InitializeLocalControlApi()
    {
        try
        {
            var cfg = _config.Load();
            if (cfg.LocalApiEnabled)
            {
                _localApi = new LocalControlApiService(_config, _usbip, _drivers, _favorites, _streamMonitor, cfg.LocalApiPort);
                _localApi.Start();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalControlApi] {ex.Message}");
        }

        _streamMonitor.StreamStarted += OnStreamStartedAsync;
        _streamMonitor.StreamStopped += OnStreamStoppedAsync;
        _streamPollTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _streamPollTimer.Tick += (_, _) =>
        {
            try
            {
                var procs = Process.GetProcesses().Select(p => p.ProcessName);
                var detected = StreamingSessionMonitor.DetectStreamingProvider(procs);
                _streamMonitor.SetStreamState(detected != null, detected);
            }
            catch { }
        };
        _streamPollTimer.Start();
    }

    private async void OnStreamStartedAsync(object? sender, string provider)
    {
        try
        {
            var cfg = _config.Load();
            if (!SessionIsolationService.ShouldAllowAutoConnect(SessionIsolationService.GetCurrentSessionInfo(), cfg.EnforceSessionIsolation))
                return;

            var targets = StreamingSessionMonitor.FilterFavoritesForStreamTransition(_favorites.GetFavorites(), true);
            foreach (var fav in targets)
            {
                var (resolvedIp, resolvedBusId, _) = await _favorites.ResolveFavoriteTargetAsync(fav);
                var op = await _usbip.ConnectAsync(resolvedIp, resolvedBusId, fav.ReceiveMode, fav.CustomSerial);
                if (op.Success)
                {
                    var connected = await _usbip.GetConnectedDevicesAsync();
                    var match = connected.FirstOrDefault(d =>
                        string.Equals(d.BusId, resolvedBusId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(d.ServerIP, resolvedIp, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        lock (_streamAttachedPorts) { _streamAttachedPorts.Add(match.Port); }
                        _sessionIsolation.RegisterPortOwner(match.Port);
                    }
                }
            }
            await Dispatcher.InvokeAsync(async () => await RefreshConnectedDevicesAsync());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StreamStarted] {ex.Message}");
        }
    }

    private async void OnStreamStoppedAsync(object? sender, string provider)
    {
        try
        {
            List<int> ports;
            lock (_streamAttachedPorts)
            {
                ports = _streamAttachedPorts.ToList();
                _streamAttachedPorts.Clear();
            }
            foreach (var port in ports)
            {
                await _usbip.DisconnectAsync(port);
                _sessionIsolation.ReleasePortOwner(port);
            }
            if (ports.Count > 0)
                await Dispatcher.InvokeAsync(async () => await RefreshConnectedDevicesAsync());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StreamStopped] {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        try { _streamPollTimer?.Stop(); _streamPollTimer = null; } catch { }
        try { _localApi?.Dispose(); _localApi = null; } catch { }
        try
        {
            _vhciListenerCts?.Cancel();
            _vhciListenerCts?.Dispose();
            _vhciListenerCts = null;
        }
        catch (Exception ignoreEx) { System.Diagnostics.Debug.WriteLine($"[MainWindow.xaml.cs] {ignoreEx.Message}"); }

        CleanupIpcAndHotkey();
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnSystemPowerModeChanged;
        _connectionMonitor.Stop();
        _connectionMonitor.Dispose();

        if (_trayIcon != null)
        {
            try
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            catch (Exception ignoreEx) { System.Diagnostics.Debug.WriteLine($"[MainWindow.xaml.cs] {ignoreEx.Message}"); }
            _trayIcon = null;
        }

        var config = _config.Load();
        config.Language = _currentLanguage;
        config.Theme = _currentTheme;
        config.DefaultReceiveMode = _currentReceiveMode;
        config.LastServerIP = txtServerIP.Text;
        config.Favorites = _favorites.GetFavorites().ToList();
        _config.Save(config);

        base.OnClosed(e);
    }

    #endregion

    #region Context Menu

    private async void MenuConnect_Click(object sender, RoutedEventArgs e)
    {
        if (treeDevices.SelectedItem is TreeViewItem node)
        {
            if (node.Tag is UsbDevice device)
            {
                await ConnectDeviceCoreAsync(device.ServerIP, device.BusId, device.Description, device.VendorId, device.ProductId);
            }
            else if (node.Tag is FavoriteDevice fav)
            {
                await ConnectDeviceCoreAsync(fav.ServerIP, fav.BusId, fav.Description, receiveMode: fav.ReceiveMode);
            }
        }
    }

    private async void MenuDisconnect_Click(object sender, RoutedEventArgs e)
    {
        if (treeDevices.SelectedItem is TreeViewItem node && node.Tag is ConnectedDevice device)
        {
            await DisconnectDeviceCoreAsync(device, node);
        }
    }

    private void MenuAddFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (treeDevices.SelectedItem is TreeViewItem node && node.Tag is UsbDevice device)
        {
            string? serverHost = null;
            if (node.Parent is TreeViewItem parentNode && parentNode.Tag is string parentHost && !string.IsNullOrWhiteSpace(parentHost))
            {
                serverHost = parentHost;
            }
            var result = _favorites.AddFavorite(
                device.ServerIP,
                device.BusId,
                device.Description,
                true,
                _currentReceiveMode,
                device.HardwareId,
                serverHostname: serverHost,
                revision: device.Revision);
            if (result.Success)
            {
                AddLocalizedLog("Success", "log_favorite_added");
                LoadFavorites();
            }
            else
            {
                AddLog("Warning", result.Error ?? "Already in favorites");
            }
        }
    }

    private void MenuRemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (treeDevices.SelectedItem is TreeViewItem node)
        {
            string serverIP = "", busId = "";

            if (node.Tag is UsbDevice device)
            {
                serverIP = device.ServerIP;
                busId = device.BusId;
            }
            else if (node.Tag is FavoriteDevice fav)
            {
                serverIP = fav.ServerIP;
                busId = fav.BusId;
            }

            if (!string.IsNullOrEmpty(serverIP))
            {
                var result = _favorites.RemoveFavorite(serverIP, busId);
                if (result.Success)
                {
                    AddLocalizedLog("Success", "log_favorite_removed");
                    LoadFavorites();
                }
            }
        }
    }

    private void MenuRemoveServer_Click(object sender, RoutedEventArgs e)
    {
        if (treeDevices.SelectedItem is TreeViewItem node && node.Parent is TreeViewItem parent)
        {
            if (parent == nodeHubs)
            {
                nodeHubs.Items.Remove(node);
                nodeHubs.Header = $"{_localization.GetText("node_hubs", _currentLanguage)} ({nodeHubs.Items.Count})";
            }
        }
    }

    private void MenuProperties_Click(object sender, RoutedEventArgs e)
    {
        if (treeDevices.SelectedItem is TreeViewItem node && node.Tag is UsbDevice device)
        {
            var vendorDesc = _usbIds.GetVendor(device.VendorId);
            var productDesc = _usbIds.GetProduct(device.VendorId, device.ProductId);

            var props = $"{_localization.GetText("props_busid", _currentLanguage)}: {device.BusId}\n" +
                        $"{_localization.GetText("props_server", _currentLanguage)}: {device.ServerIP}\n" +
                        $"{_localization.GetText("props_desc", _currentLanguage)}: {device.Description}\n" +
                        $"{_localization.GetText("props_vidpid", _currentLanguage)}: {device.VendorId}:{device.ProductId}\n" +
                        $"{_localization.GetText("props_vendor", _currentLanguage)}: {vendorDesc}\n" +
                        $"{_localization.GetText("props_product", _currentLanguage)}: {productDesc}";
            System.Windows.MessageBox.Show(props, _localization.GetText("props_title", _currentLanguage), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }


    private string? GetCustomSerialForDevice(string? hwId, string serverIP, string busId)
    {
        var cfg = _config.Load();
        if (cfg.CustomDeviceSerials == null) return null;
        string key = !string.IsNullOrWhiteSpace(hwId) ? hwId : $"{serverIP}|{busId}";
        if (cfg.CustomDeviceSerials.TryGetValue(key, out var s) && !string.IsNullOrWhiteSpace(s))
            return s.Trim();
        if (cfg.CustomDeviceSerials.TryGetValue($"{serverIP}|{busId}", out var s2) && !string.IsNullOrWhiteSpace(s2))
            return s2.Trim();
        return null;
    }

    private void MenuSetSerial_Click(object sender, RoutedEventArgs e)
    {
        if (treeDevices.SelectedItem is not TreeViewItem node || node.Tag is not UsbDevice device) return;
        string key = !string.IsNullOrWhiteSpace(device.HardwareId) ? device.HardwareId : $"{device.ServerIP}|{device.BusId}";
        string current = GetCustomSerialForDevice(device.HardwareId, device.ServerIP, device.BusId) ?? VhciNativeClient.GenerateUsbSerial();
        var prompt = _currentLanguage == "es"
            ? $"Serial USB ASCII (1-15 alfanumérico, vacío para quitar) para {device.Description}:"
            : $"ASCII USB Serial (1-15 alphanumeric, blank to clear) for {device.Description}:";
        var input = Microsoft.VisualBasic.Interaction.InputBox(prompt, _currentLanguage == "es" ? "Número de serie USB" : "Custom USB Serial", current);
        if (input == null) return;
        string trimmed = input.Trim();
        if (!VhciNativeClient.IsValidUsbSerial(trimmed))
        {
            lblStatus.Text = _currentLanguage == "es" ? "Serial inválido (1-15 ASCII)" : "Invalid serial (1-15 ASCII)";
            return;
        }
        var cfg = _config.Load();
        cfg.CustomDeviceSerials ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(trimmed)) cfg.CustomDeviceSerials.Remove(key);
        else cfg.CustomDeviceSerials[key] = trimmed;
        _config.Save(cfg);
        bool isEs = _currentLanguage == "es";
        AddLog("Success", string.IsNullOrEmpty(trimmed)
            ? (isEs ? $"Número de serie personalizado eliminado para {device.BusId}" : $"Cleared custom serial for {device.BusId}")
            : (isEs ? $"Número de serie personalizado '{trimmed}' guardado para {device.BusId}" : $"Set custom serial '{trimmed}' for {device.BusId}"));
    }

    private async void MenuToggleKernelPersist_Click(object sender, RoutedEventArgs e)
    {
        if (treeDevices.SelectedItem is not TreeViewItem node || node.Tag is not UsbDevice device) return;
        var current = await _usbip.GetKernelPersistentDevicesAsync();
        bool exists = current.Any(p => string.Equals(p.Host, device.ServerIP, StringComparison.OrdinalIgnoreCase) && string.Equals(p.BusId, device.BusId, StringComparison.OrdinalIgnoreCase));
        var updated = current.Where(p => !(string.Equals(p.Host, device.ServerIP, StringComparison.OrdinalIgnoreCase) && string.Equals(p.BusId, device.BusId, StringComparison.OrdinalIgnoreCase))).ToList();
        if (!exists)
        {
            string serial = GetCustomSerialForDevice(device.HardwareId, device.ServerIP, device.BusId) ?? string.Empty;
            updated.Add(new VhciPersistentDevice(device.ServerIP, VhciNativeClient.DefaultServicePort, device.BusId, serial, _currentReceiveMode, AttachOnce: false));
        }
        var res = await _usbip.SetKernelPersistentDevicesAsync(updated);
        bool isEs = _currentLanguage == "es";
        if (res.Success)
        {
            AddLog("Success", exists
                ? (isEs ? $"Eliminado {device.BusId} de la persistencia de arranque del kernel" : $"Removed {device.BusId} from kernel boot persistence")
                : (isEs ? $"Fijado {device.BusId} ({device.ServerIP}) a la persistencia de arranque del kernel" : $"Pinned {device.BusId} ({device.ServerIP}) to kernel boot persistence"));
            lblStatus.Text = exists
                ? (isEs ? "Desfijado del arranque" : "Unpinned from boot")
                : (isEs ? "Fijado al arranque del kernel" : "Pinned to kernel boot");
        }
        else
        {
            AddLog("Warning", res.Error ?? (isEs ? "Fallo al actualizar la persistencia del kernel" : "Kernel persistence update failed"));
        }
    }

    private async void MenuStopAttachRetries_Click(object sender, RoutedEventArgs e)
    {
        string? host = null;
        string? busId = null;
        if (treeDevices.SelectedItem is TreeViewItem node && node.Tag is UsbDevice device)
        {
            host = device.ServerIP;
            busId = device.BusId;
        }
        int canceled = await _usbip.StopKernelAttachAttemptsAsync(host, busId);
        bool isEs = _currentLanguage == "es";
        AddLog("Info", isEs
            ? $"Detenido(s) {canceled} reintento(s) de conexión del kernel."
            : $"Stopped {canceled} pending kernel attach attempt(s).");
        lblStatus.Text = isEs ? $"Cancelados: {canceled}" : $"Canceled: {canceled}";
    }

    private void MenuConfigureHubPorts_Click(object sender, RoutedEventArgs e)
    {
        var cur = KernelConfigService.GetVirtualHubPortSettings();
        bool isEs = _currentLanguage == "es";
        var prompt = isEs
            ? $"Puertos USB 2.0, USB 3.0 (total <= 255). Actual: {cur.Usb20Ports},{cur.Usb30Ports}"
            : $"USB 2.0, USB 3.0 port counts (total <= 255). Current: {cur.Usb20Ports},{cur.Usb30Ports}";
        var input = Microsoft.VisualBasic.Interaction.InputBox(prompt, isEs ? "Puertos Hub Virtual" : "Virtual Hub Ports", $"{cur.Usb20Ports},{cur.Usb30Ports}");
        if (string.IsNullOrWhiteSpace(input)) return;
        var parts = input.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int u20) && int.TryParse(parts[1].Trim(), out int u30))
        {
            var res = KernelConfigService.SetVirtualHubPortSettings(u20, u30);
            AddLog(res.Success ? "Success" : "Warning", res.Success
                ? (isEs ? $"Puertos del hub actualizados a {u20}/{u30} (reinicia para aplicar)" : $"Hub ports updated to {u20}/{u30} (reboot to apply)")
                : (res.Error ?? (isEs ? "Error al actualizar puertos" : "Failed")));
        }
        else
        {
            string msg = isEs ? "Formato inválido. Usa 'USB20,USB30' (ej. 30,30)." : "Invalid format. Use 'USB20,USB30' (e.g. 30,30).";
            AddLog("Warning", msg);
            lblStatus.Text = isEs ? "Formato de puertos inválido" : "Invalid port format";
        }
    }

    private async void MenuExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var connected = await _usbip.GetConnectedDevicesAsync();
        var persistent = await _usbip.GetKernelPersistentDevicesAsync();
        var zipPath = KernelDiagnosticsService.ExportDiagnosticBundle(
            recentUiLogs: txtLog.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
            driverStatusText: lblDriverValue.Text,
            connectedDevices: connected,
            persistentDevices: persistent);
        bool isEs = _currentLanguage == "es";
        AddLog("Success", isEs ? $"Diagnóstico exportado: {zipPath}" : $"Diagnostics exported: {zipPath}");
        lblStatus.Text = System.IO.Path.GetFileName(zipPath);
    }

    private void MenuRename_Click(object sender, RoutedEventArgs e)
    {
        if (treeDevices.SelectedItem is TreeViewItem node && node.Tag is UsbDevice device)
        {
            var currentName = _usbIds.GetDeviceDescription(device.VendorId, device.ProductId);

            var prompt = _localization.GetText("rename_prompt", _currentLanguage, device.VendorId, device.ProductId);
            var title = _localization.GetText("rename_title", _currentLanguage);

            var newName = Microsoft.VisualBasic.Interaction.InputBox(
                prompt,
                title,
                currentName);

            if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
            {
                _usbIds.SetCustomName(device.VendorId, device.ProductId, newName);
                device.Description = newName;

                var isConnected = node.Header?.ToString()?.StartsWith("☑️") == true;
                node.Header = isConnected
                    ? $"☑️ {device.BusId}: {newName}"
                    : $"🔌 {device.BusId}: {newName}";

                var config = _config.Load();
                config.CustomDeviceNames ??= new Dictionary<string, string>();
                config.CustomDeviceNames[$"{device.VendorId}:{device.ProductId}"] = newName;
                _config.Save(config);

                AddLocalizedLog("Success", "log_device_renamed", newName);
            }
        }
    }

    #endregion

    #region Connection Monitor Events

    private void OnConnectionLost(object? sender, ConnectionLostEventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            AddLocalizedLog("Warning", "log_connection_lost_detail", e.ServerIP, e.BusId);
            ShowNotification(
                _localization.GetText("notify_connection_lost", _currentLanguage),
                $"{e.BusId} @ {e.ServerIP}",
                Forms.ToolTipIcon.Warning);
            lblStatus.Text = $"⚠️ {_localization.GetText("notify_connection_lost", _currentLanguage)}: {e.BusId}";
            lblStatus.Foreground = (SolidColorBrush)FindResource("StatusWarning");
        });
    }

    private void OnConnectionRestored(object? sender, ConnectionRestoredEventArgs e)
    {
        _ = Dispatcher.InvokeAsync(async () =>
        {
            await RefreshConnectedDevicesAsync();
            await RefreshServerDevicesAsync();
            AddLocalizedLog("Success", "log_connection_restored_detail", e.ServerIP, e.BusId);
            ShowNotification(
                _localization.GetText("notify_reconnected", _currentLanguage),
                $"{e.BusId} @ {e.ServerIP}",
                Forms.ToolTipIcon.Info);
            lblStatus.Text = $"✓ {_localization.GetText("notify_reconnected", _currentLanguage)}: {e.BusId}";
            lblStatus.Foreground = (SolidColorBrush)FindResource("StatusSuccess");
        });
    }

    #endregion

    #region Global Hotkey & Headless CLI IPC

    internal static bool TryParseHotkey(string? hotkey, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(hotkey)) return false;

        foreach (var rawToken in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = rawToken.ToUpperInvariant();
            switch (token)
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= MOD_CONTROL;
                    break;
                case "ALT":
                    modifiers |= MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= MOD_SHIFT;
                    break;
                default:
                    if (token.Length == 1 && ((token[0] >= 'A' && token[0] <= 'Z') || (token[0] >= '0' && token[0] <= '9')))
                    {
                        virtualKey = token[0];
                    }
                    else if (token.StartsWith('F') && int.TryParse(token[1..], out int fNum) && fNum is >= 1 and <= 24)
                    {
                        virtualKey = (uint)(0x70 + fNum - 1);
                    }
                    break;
            }
        }

        return modifiers != 0 && virtualKey != 0;
    }

    private void InitializeIpcAndHotkey()
    {
        try
        {
            _disconnectAllEvent = new EventWaitHandle(false, EventResetMode.AutoReset, App.DisconnectAllEventName);
            _disconnectAllWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _disconnectAllEvent,
                (_, _) => _ = Dispatcher.InvokeAsync(() => DisconnectAllDevicesFromHotkeyOrCliAsync("CLI")),
                null,
                Timeout.Infinite,
                false);

            _gracefulExitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, App.GracefulExitEventName);
            _gracefulExitWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _gracefulExitEvent,
                (_, _) => _ = Dispatcher.InvokeAsync(GracefulShutdownAsync),
                null,
                Timeout.Infinite,
                false);

            _ipcPipeCts = new CancellationTokenSource();
            _ = RunIpcPipeServerAsync(_ipcPipeCts.Token);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[IPC Init] {ex.Message}");
        }

        try
        {
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            if (hwnd != IntPtr.Zero)
            {
                _hwndSource = HwndSource.FromHwnd(hwnd);
                _hwndSource?.AddHook(WndProc);

                if (!TryParseHotkey(_config.Load().DisconnectAllHotkey, out uint mods, out uint vk))
                {
                    mods = MOD_CONTROL | MOD_ALT;
                    vk = 0x44; // 'D'
                }

                _hotkeyRegistered = RegisterHotKey(hwnd, HOTKEY_ID_DISCONNECT_ALL, mods | MOD_NOREPEAT, vk);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Hotkey Init] {ex.Message}");
        }
    }

    private void CleanupIpcAndHotkey()
    {
        try
        {
            if (_hwndSource != null)
            {
                if (_hotkeyRegistered && _hwndSource.Handle != IntPtr.Zero)
                {
                    UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID_DISCONNECT_ALL);
                    _hotkeyRegistered = false;
                }
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
        }
        catch (Exception ignoreEx) { System.Diagnostics.Debug.WriteLine($"[MainWindow.xaml.cs] {ignoreEx.Message}"); }

        try
        {
            _disconnectAllWaitHandle?.Unregister(null);
            _disconnectAllWaitHandle = null;
            _disconnectAllEvent?.Dispose();
            _disconnectAllEvent = null;

            _gracefulExitWaitHandle?.Unregister(null);
            _gracefulExitWaitHandle = null;
            _gracefulExitEvent?.Dispose();
            _gracefulExitEvent = null;

            _ipcPipeCts?.Cancel();
            _ipcPipeCts?.Dispose();
            _ipcPipeCts = null;
        }
        catch (Exception ignoreEx) { System.Diagnostics.Debug.WriteLine($"[MainWindow.xaml.cs] {ignoreEx.Message}"); }
    }

    private async Task RunIpcPipeServerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new System.IO.Pipes.NamedPipeServerStream(
                    App.IpcPipeName,
                    System.IO.Pipes.PipeDirection.In,
                    1,
                    System.IO.Pipes.PipeTransmissionMode.Byte,
                    System.IO.Pipes.PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(ct);
                using var reader = new System.IO.StreamReader(server, System.Text.Encoding.UTF8);
                string? line = await reader.ReadLineAsync(ct);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split('|', 3, StringSplitOptions.TrimEntries);
                    if (parts.Length == 3 &&
                        parts[0].Equals("CONNECT", StringComparison.OrdinalIgnoreCase) &&
                        USBAnywhere.Utils.InputValidator.IsValidHost(parts[1]) &&
                        USBAnywhere.Utils.InputValidator.IsValidBusId(parts[2]))
                    {
                        await Dispatcher.InvokeAsync(() => ConnectCliDeviceAsync(parts[1], parts[2]));
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IPC Pipe] {ex.Message}");
                await Task.Delay(250, ct);
            }
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_DISCONNECT_ALL)
        {
            handled = true;
            _ = Dispatcher.InvokeAsync(() => DisconnectAllDevicesFromHotkeyOrCliAsync("Hotkey"));
        }
        return IntPtr.Zero;
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        if (e.Key == Key.D &&
            mods.HasFlag(ModifierKeys.Control) &&
            (mods.HasFlag(ModifierKeys.Shift) || mods.HasFlag(ModifierKeys.Alt)))
        {
            e.Handled = true;
            _ = DisconnectAllDevicesFromHotkeyOrCliAsync("Hotkey");
        }
    }

    private async Task RefreshConnectedDevicesAsync()
    {
        try
        {
            var connectedDevices = await _usbip.GetConnectedDevicesAsync();
            nodeConnected.Items.Clear();
            foreach (var device in connectedDevices)
            {
                AddDeviceToConnectedTree(device);
            }
            UpdateConnectedCount();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RefreshConnectedDevices] {ex.Message}");
        }
    }

    private Task RefreshServerDevicesAsync()
    {
        try
        {
            foreach (TreeViewItem hubNode in nodeHubs.Items)
            {
                string serverIp = string.Empty;
                var match = System.Text.RegularExpressions.Regex.Match(
                    hubNode.Header?.ToString() ?? string.Empty,
                    @"🖥️\s+([a-zA-Z0-9\.\-]+)");
                if (match.Success) serverIp = match.Groups[1].Value;

                foreach (TreeViewItem deviceNode in hubNode.Items)
                {
                    if (deviceNode.Tag is UsbDevice dev)
                    {
                        var ip = string.IsNullOrEmpty(dev.ServerIP) ? serverIp : dev.ServerIP;
                        var icon = IsDeviceInConnectedNode(ip, dev.BusId) ? "☑️" : "🔌";
                        deviceNode.Header = $"{icon} {dev.BusId}: {dev.Description}";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RefreshServerDevices] {ex.Message}");
        }
        return Task.CompletedTask;
    }

    private async Task DisconnectAllDevicesFromHotkeyOrCliAsync(string triggerSource)
    {
        if (!await _disconnectAllLock.WaitAsync(0))
            return;

        try
        {
            // Clear background reconnect monitor first so devices stay unplugged
            _connectionMonitor.ClearAll();

            int detached = await _usbip.DetachAllConnectedDevicesAsync();
            await RefreshConnectedDevicesAsync();
            await RefreshServerDevicesAsync();

            AddLocalizedLog("Info", "log_disconnect_all_done", detached, triggerSource);
            lblStatus.Text = $"✓ {_localization.GetText("log_disconnected", _currentLanguage)} ({detached})";
            lblStatus.Foreground = (SolidColorBrush)FindResource("StatusSuccess");

            ShowNotification(
                _localization.GetText("log_disconnected", _currentLanguage),
                _localization.GetText("log_disconnect_all_done", _currentLanguage, detached, triggerSource),
                Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            AddLog("Error", ex.Message);
        }
        finally
        {
            _disconnectAllLock.Release();
        }
    }

    private void StartVhciDeviceStateListener()
    {
        if (_vhciListenerCts != null)
            return;

        var cts = new CancellationTokenSource();
        _vhciListenerCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await VhciNativeClient.RunDeviceStateListenerAsync(evt =>
                {
                    long seq = Interlocked.Increment(ref _vhciRefreshVersion);
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(120, cts.Token).ConfigureAwait(false);
                        if (cts.IsCancellationRequested || Interlocked.Read(ref _vhciRefreshVersion) != seq)
                            return;

                        await Dispatcher.InvokeAsync(async () =>
                        {
                            await RefreshConnectedDevicesAsync();
                            await RefreshServerDevicesAsync();
                        });
                    }, cts.Token);
                }, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VhciListener] {ex.Message}");
            }
        });
    }

    #endregion
}
