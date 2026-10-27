// ═══════════════════════════════════════════════════════════════════════════════
// USBAnywhere - USB/IP Client for Windows
// Copyright (c) 2025-2026 SnakeFoxu - https://github.com/oscarlosu-lab/USBAnywhere
// Licensed under GPL v3 - See LICENSE file
// ═══════════════════════════════════════════════════════════════════════════════

using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using USBAnywhere.Models;
using USBAnywhere.Services;
using USBAnywhere.Services.Interfaces;
using Forms = System.Windows.Forms;

namespace USBAnywhere;

/// <summary>
/// MainWindow - USB/IP Manager
/// Created by SnakeFoxu - github.com/oscarlosu-lab
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
    private string _currentReceiveMode = "zero-copy";
    private record LogEntry(DateTime Timestamp, string Type, string? Key, object[] Args, string? RawMessage);
    private readonly List<LogEntry> _logEntries = new();
    private Forms.ToolStripMenuItem? _trayScanItem;
    private Forms.ToolStripMenuItem? _trayShowItem;
    private Forms.ToolStripMenuItem? _trayExitItem;
    private List<string> _availableSubnets = new();

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
        _localization      = localization;
        _config            = config;
        _networkScanner    = networkScanner;
        _usbip             = usbip;
        _drivers           = drivers;
        _favorites         = favorites;
        _updates           = updates;
        _vpn               = vpn;
        _usbIds            = usbIds;
        _connectionMonitor = connectionMonitor;
        _notificationService = notificationService ?? new NotificationService();

        InitializeComponent();

        // Subscribe to connection events
        _connectionMonitor.ConnectionLost     += OnConnectionLost;
        _connectionMonitor.ConnectionRestored += OnConnectionRestored;

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

        // Auto-scan network on startup
        Loaded += async (s, e) => await AutoScanOnStartup();
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
                    loadedIcon = new Icon(s, new System.Drawing.Size(32, 32));
                }
            }
            catch { }

            // 2. Fallback to Assets/icon.ico on disk
            if (loadedIcon == null)
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    using var iconStream = new System.IO.FileStream(iconPath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                    loadedIcon = new Icon(iconStream, new System.Drawing.Size(32, 32));
                }
            }

            // 3. Fallback to executable's associated Win32 icon
            if (loadedIcon == null && !string.IsNullOrEmpty(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
            {
                try { loadedIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }
            }

            _trayIcon.Icon = loadedIcon ?? System.Drawing.SystemIcons.Application;

            // Create context menu
            var trayMenu = new Forms.ContextMenuStrip();

            _trayScanItem = new Forms.ToolStripMenuItem(_localization.GetText("tray_scan", _currentLanguage));
            _trayScanItem.Click += (s, e) => Dispatcher.Invoke(() => BtnScan_Click(this, new RoutedEventArgs()));
            trayMenu.Items.Add(_trayScanItem);

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
            AddLog("Warning", string.Format(_localization.GetText("log_tray_init_error", _currentLanguage), ex.Message));
        }
    }

    /// <summary>
    /// Show the main window from tray
    /// </summary>
    private void ShowWindow()
    {
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
            Hide();
            ShowNotification(_localization.GetText("title_main", _currentLanguage),
                           _localization.GetText("tray_minimized", _currentLanguage),
                           Forms.ToolTipIcon.Info);
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
                nodeHubs.Header = $"🖥️ USB Hubs ({servers.Count})";

                txtServerIP.Text = servers[0];

                AddLog("Success", $"{servers.Count} {_localization.GetText("log_scan_found", _currentLanguage)}");
                lblStatus.Text = $"✓ {servers.Count} {_localization.GetText("status_servers_found", _currentLanguage)}";
                lblStatus.Foreground = (SolidColorBrush)FindResource("StatusSuccess");

                foreach (var server in servers)
                {
                    await ListDevicesOnServer(server);
                }

                await LoadConnectedDevicesAsync();
                await AutoConnectFavorites();
            }
            else
            {
                lblStatus.Text = _localization.GetText("status_no_server", _currentLanguage);
                lblStatus.Foreground = (SolidColorBrush)FindResource("StatusWarning");
                AddLocalizedLog("Warning", "log_scan_none");
                await LoadConnectedDevicesAsync();
            }

            // Support CLI --connect <IP> <BUSID> for Moonlight / Sunshine / Playnite automation
            var cliArgs = Environment.GetCommandLineArgs();
            for (int i = 0; i < cliArgs.Length - 2; i++)
            {
                if (cliArgs[i].Equals("--connect", StringComparison.OrdinalIgnoreCase))
                {
                    var cliIp = cliArgs[i + 1];
                    var cliBusId = cliArgs[i + 2];
                    if (USBAnywhere.Utils.InputValidator.IsValidHost(cliIp) && USBAnywhere.Utils.InputValidator.IsValidBusId(cliBusId))
                    {
                        var mode = _config.Load().DefaultReceiveMode ?? "zero-copy";
                        await _usbip.AttachDeviceAsync(cliIp, cliBusId, mode);
                        await LoadConnectedDevicesAsync();
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            AddLog("Error", ex.Message);
        }
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
                    var usbIdsName = _usbIds.GetDeviceDescription(device.VendorId, device.ProductId);
                    if (!usbIdsName.StartsWith("Unknown") && !usbIdsName.StartsWith("USB Device"))
                    {
                        displayName = usbIdsName;
                        device.Description = displayName;
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
            nodeHubs.Header = $"🖥️ USB Hubs ({nodeHubs.Items.Count})";

            AddLocalizedLog("Info", "log_devices_on_server", devices.Count, serverIP);
        }
        catch (Exception ex)
        {
            AddLog("Error", ex.Message);
        }
    }

    private static async Task<double> MeasureServerLatencyMsAsync(string serverIP)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(500);
            await client.ConnectAsync(serverIP, 3240, cts.Token);
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }
        catch
        {
            return -1;
        }
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

    /// <summary>
    /// Auto-connect favorites with AutoConnect=true
    /// </summary>
    private async Task AutoConnectFavorites()
    {
        var favorites = _favorites.GetFavorites().Where(f => f.AutoConnect).ToList();
        if (favorites.Count == 0) return;

        AddLocalizedLog("Info", "log_auto_connecting", favorites.Count);

        foreach (var fav in favorites)
        {
            if (IsDeviceInConnectedNode(fav.ServerIP, fav.BusId))
                continue;

            try
            {
                var receiveMode = !string.IsNullOrWhiteSpace(fav.ReceiveMode) ? fav.ReceiveMode : _currentReceiveMode;
                var result = await _usbip.ConnectAsync(fav.ServerIP, fav.BusId, receiveMode);
                if (result.Success)
                {
                    AddLocalizedLog("Success", "log_connected_favorite", fav.Description, fav.BusId);
                    AddDeviceToConnectedTree(new ConnectedDevice
                    {
                        ServerIP = fav.ServerIP,
                        BusId = fav.BusId,
                        Description = fav.Description
                    });
                    _connectionMonitor.AddDevice(fav.ServerIP, fav.BusId, autoReconnect: true);
                    _connectionMonitor.Start();
                }
                else
                {
                    AddLocalizedLog("Warning", "log_connection_failed", fav.BusId, result.Error ?? "");
                }
            }
            catch (Exception ex)
            {
                AddLocalizedLog("Error", "log_connection_error", fav.BusId, ex.Message);
            }
        }
    }

    private async Task ConnectDeviceCoreAsync(string serverIP, string busId, string description, string vendorId = "", string productId = "", string? receiveMode = null)
    {
        var mode = !string.IsNullOrWhiteSpace(receiveMode) ? receiveMode : _currentReceiveMode;
        AddLocalizedLog("Info", "log_connecting_device", busId, serverIP);
        lblStatus.Text = $"{_localization.GetText("status_connecting", _currentLanguage)} {busId}...";
        lblStatus.Foreground = (SolidColorBrush)FindResource("StatusInfo");

        var result = await _usbip.ConnectAsync(serverIP, busId, mode);

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

            _connectionMonitor.AddDevice(serverIP, busId, autoReconnect: true);
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
            var result = await _usbip.DisconnectAsync(portDevice.Port);

            if (result.Success)
            {
                AddLocalizedLog("Success", "log_disconnected_port", portDevice.Port);
                lblStatus.Text = $"✓ {_localization.GetText("log_disconnected", _currentLanguage)}";
                lblStatus.Foreground = (SolidColorBrush)FindResource("StatusSuccess");

                _connectionMonitor.RemoveDevice(connectedDevice.ServerIP, connectedDevice.BusId);
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
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
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
            try { DragMove(); } catch { }
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private async void BtnClose_Click(object sender, RoutedEventArgs e) => await GracefulShutdownAsync();

    private async Task GracefulShutdownAsync()
    {
        _isShuttingDown = true;
        try
        {
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

    private void ApplyTheme(string theme)
    {
        btnTheme.Content = theme == "dark" ? "🌙" : "☀️";
        var resources = System.Windows.Application.Current.Resources;

        if (theme == "light")
        {
            resources["BackgroundDark"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F5F5"));
            resources["BackgroundPanel"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
            resources["BackgroundTitleBar"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8E8E8"));
            resources["BackgroundButton"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DEDEDE"));
            resources["BackgroundButtonHover"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#C8C8C8"));
            resources["BackgroundInput"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"));
            resources["ForegroundPrimary"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E"));
            resources["ForegroundSecondary"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));
            resources["AccentOrange"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D35400"));
            resources["AccentBlue"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0078D4"));
            resources["StatusSuccess"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#107C10"));
            resources["StatusInfo"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0078D4"));
        }
        else
        {
            resources["BackgroundDark"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E"));
            resources["BackgroundPanel"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252526"));
            resources["BackgroundTitleBar"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#323233"));
            resources["BackgroundButton"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0E639C"));
            resources["BackgroundButtonHover"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1177BB"));
            resources["BackgroundInput"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3C3C3C"));
            resources["ForegroundPrimary"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DCDCDC"));
            resources["ForegroundSecondary"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#969696"));
            resources["AccentOrange"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CE9178"));
            resources["AccentBlue"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0E639C"));
            resources["StatusSuccess"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89D185"));
            resources["StatusInfo"] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#75BEFF"));
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

        // Localize Tray Menu
        if (_trayScanItem != null) _trayScanItem.Text = _localization.GetText("tray_scan", _currentLanguage);
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
            nodeHubs.Header = $"🖥️ USB Hubs ({servers.Count})";

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
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                    !ni.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var parts = ip.Address.ToString().Split('.');
                            return $"{parts[0]}.{parts[1]}.{parts[2]}";
                        }
                    }
                }
            }
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
        if (string.IsNullOrEmpty(serverIP)) serverIP = "192.168.1.100";

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

                var prompt = string.Format(_localization.GetText("msg_update_prompt", _currentLanguage), updateInfo.LatestVersion, updateInfo.ReleaseNotes);
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
        _currentReceiveMode = string.IsNullOrWhiteSpace(config.DefaultReceiveMode) ? "zero-copy" : config.DefaultReceiveMode;

        if (!string.IsNullOrEmpty(config.LastServerIP))
            txtServerIP.Text = config.LastServerIP;

        _usbIds.LoadCustomNames(config.CustomDeviceNames);
    }

    protected override void OnClosed(EventArgs e)
    {
        _connectionMonitor.Stop();
        _connectionMonitor.Dispose();

        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
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
            var result = _favorites.AddFavorite(device.ServerIP, device.BusId, device.Description, true);
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
                nodeHubs.Header = $"🖥️ USB Hubs ({nodeHubs.Items.Count})";
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

    private void MenuRename_Click(object sender, RoutedEventArgs e)
    {
        if (treeDevices.SelectedItem is TreeViewItem node && node.Tag is UsbDevice device)
        {
            var currentName = _usbIds.GetDeviceDescription(device.VendorId, device.ProductId);

            var prompt = string.Format(_localization.GetText("rename_prompt", _currentLanguage), device.VendorId, device.ProductId);
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
        Dispatcher.Invoke(() =>
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
        Dispatcher.Invoke(() =>
        {
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
}
