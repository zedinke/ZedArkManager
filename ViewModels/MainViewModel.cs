using System.Collections.ObjectModel;
using System.Windows.Input;
using ZedASAManager.Models;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly SshService _sshService;
    private readonly SettingsService _settingsService;
    private readonly ServerDiscoveryService _discoveryService;
    private readonly MonitoringService _monitoringService;
    private readonly UserService _userService;
    private readonly ServerDataService _serverDataService;
    private readonly UpdateService _updateService;

    private ConnectionSettings? _currentConnection;
    private bool _isConnected;
    private string _globalLog = string.Empty;
    private bool _isLogExpanded;
    private ChartViewModel? _chartViewModel;
    private string _currentUsername = string.Empty;
    private SavedServer? _selectedSavedServer;
    private ObservableCollection<SavedServer> _savedServers = new();
    private string _updateButtonText = "Frissítés";

    public MainViewModel(string username)
    {
        _currentUsername = username;
        _sshService = new SshService();
        _settingsService = new SettingsService();
        _userService = new UserService();
        _serverDataService = new ServerDataService();
        _discoveryService = new ServerDiscoveryService(_sshService);
        _monitoringService = new MonitoringService(_sshService);
        _updateService = new UpdateService();

        Servers = new ObservableCollection<ServerCardViewModel>();

        ConnectCommand = new RelayCommand(async () => await ConnectAsync());
        DisconnectCommand = new RelayCommandSync(() => Disconnect(), () => IsConnected);
        StartAllCommand = new RelayCommand(async () => await ExecuteClusterActionAsync("start"), () => IsConnected);
        StopAllCommand = new RelayCommand(async () => await ExecuteClusterActionAsync("stop"), () => IsConnected);
        UpdateAllCommand = new RelayCommand(async () => await ExecuteClusterActionAsync("update"), () => IsConnected);
        OpenSettingsCommand = new RelayCommandSync(() => OpenSettings());
        OpenClusterManagementCommand = new RelayCommandSync(() => OpenClusterManagement());
        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
        AddServerCommand = new RelayCommand(async () => await AddServerAsync(), () => IsConnected);
        RemoveServerCommand = new RelayCommand<ServerCardViewModel>(async (vm) => await RemoveServerAsync(vm), () => IsConnected);
        AddSavedServerCommand = new RelayCommandSync(() => AddSavedServer());
        RemoveSavedServerCommand = new RelayCommandSync(() => RemoveSavedServer(), () => SelectedSavedServer != null);
        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => IsConnected);
        _sshService.OutputReceived += OnSshOutputReceived;
        _sshService.ErrorReceived += OnSshErrorReceived;
        _sshService.ConnectionLost += OnSshConnectionLost;
        _monitoringService.ServerStatsUpdated += OnServerStatsUpdated;
        _monitoringService.StatusOutputReceived += OnStatusOutputReceived;

        ChartViewModel = new ChartViewModel();

        LoadSavedServers();
        LoadUpdateButtonText();
    }

    private void LoadUpdateButtonText()
    {
        UpdateButtonText = Utilities.LocalizationHelper.GetString("update");
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var (hasUpdate, isRequired, latestVersion, releaseNotes) = await _updateService.CheckForUpdatesAsync();

            if (hasUpdate)
            {
                var currentVersion = _updateService.GetCurrentVersion();
                var updateWindow = new Views.UpdateWindow(_updateService, isRequired, currentVersion, latestVersion ?? "unknown", releaseNotes);
                updateWindow.ShowDialog();
            }
            else
            {
                System.Windows.MessageBox.Show(
                    Utilities.LocalizationHelper.GetString("no_updates"),
                    Utilities.LocalizationHelper.GetString("update"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"{Utilities.LocalizationHelper.GetString("update_error")}: {ex.Message}",
                Utilities.LocalizationHelper.GetString("error"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    public ObservableCollection<ServerCardViewModel> Servers { get; }

    public ConnectionSettings? CurrentConnection
    {
        get => _currentConnection;
        set => SetProperty(ref _currentConnection, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetProperty(ref _isConnected, value))
            {
                ((RelayCommandSync)DisconnectCommand).RaiseCanExecuteChanged();
                if (StartAllCommand is RelayCommand startCmd) startCmd.RaiseCanExecuteChanged();
                if (StopAllCommand is RelayCommand stopCmd) stopCmd.RaiseCanExecuteChanged();
                if (UpdateAllCommand is RelayCommand updateCmd) updateCmd.RaiseCanExecuteChanged();
                if (AddServerCommand is RelayCommand addServerCmd) addServerCmd.RaiseCanExecuteChanged();
                if (RemoveServerCommand is RelayCommand<ServerCardViewModel> removeServerCmd) removeServerCmd.RaiseCanExecuteChanged();
                if (RefreshCommand is RelayCommand refreshCmd) refreshCmd.RaiseCanExecuteChanged();
            }
        }
    }

    public string GlobalLog
    {
        get => _globalLog;
        set => SetProperty(ref _globalLog, value);
    }

    public bool IsLogExpanded
    {
        get => _isLogExpanded;
        set => SetProperty(ref _isLogExpanded, value);
    }

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand StartAllCommand { get; }
    public ICommand AddServerCommand { get; }
    public ICommand RemoveServerCommand { get; }

    public ChartViewModel ChartViewModel
    {
        get => _chartViewModel!;
        set => SetProperty(ref _chartViewModel, value);
    }
    public ICommand StopAllCommand { get; }
    public ICommand UpdateAllCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenClusterManagementCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand AddSavedServerCommand { get; }
    public ICommand RemoveSavedServerCommand { get; }
    public ICommand RefreshCommand { get; }

    public string CurrentUsername
    {
        get => _currentUsername;
        set => SetProperty(ref _currentUsername, value);
    }

    public string UpdateButtonText
    {
        get => _updateButtonText;
        set => SetProperty(ref _updateButtonText, value);
    }

    public ObservableCollection<SavedServer> SavedServers
    {
        get => _savedServers;
        set => SetProperty(ref _savedServers, value);
    }

    public SavedServer? SelectedSavedServer
    {
        get => _selectedSavedServer;
        set
        {
            if (SetProperty(ref _selectedSavedServer, value))
            {
                ((RelayCommandSync)RemoveSavedServerCommand).RaiseCanExecuteChanged();
                if (value != null)
                {
                    LoadServerConnection(value);
                }
            }
        }
    }

    private void LoadSavedServers()
    {
        var servers = _serverDataService.LoadServers(_currentUsername);
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            SavedServers.Clear();
            foreach (var server in servers)
            {
                SavedServers.Add(server);
            }
            
            // Automatikusan kiválasztjuk az első szervert, ha van
            if (SavedServers.Count > 0 && SelectedSavedServer == null)
            {
                SelectedSavedServer = SavedServers[0];
            }
        });
    }

    private void LoadServerConnection(SavedServer server)
    {
        CurrentConnection = _serverDataService.ConvertToConnectionSettings(server);
    }

    private void AddSavedServer()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.ServerSettingsWindow();
            var result = dialog.ShowDialog();

            if (result == true && dialog.SavedServer != null)
            {
                _serverDataService.AddServer(_currentUsername, dialog.SavedServer);
                LoadSavedServers();
                
                // Automatikusan betöltjük az új szervert
                SelectedSavedServer = dialog.SavedServer;
            }
        });
    }

    private void RemoveSavedServer()
    {
        if (SelectedSavedServer == null)
            return;

        var result = System.Windows.MessageBox.Show(
            $"Biztosan törölni szeretnéd a '{SelectedSavedServer.Name}' szervert?",
            "Szerver törlése",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            _serverDataService.RemoveServer(_currentUsername, SelectedSavedServer.Name);
            LoadSavedServers();
            SelectedSavedServer = null;
            CurrentConnection = null;
        }
    }

    public async Task ConnectAsync()
    {
        if (CurrentConnection == null)
        {
            if (SelectedSavedServer == null)
            {
                System.Windows.MessageBox.Show(
                    "Kérjük, válassz ki egy szervert a legördülő menüből, vagy adj hozzá egy újat!",
                    "Nincs kiválasztott szerver",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }
            LoadServerConnection(SelectedSavedServer);
        }

        try
        {
            AppendToLog(LocalizationHelper.GetString("connecting"));
            bool connected = await _sshService.ConnectAsync(CurrentConnection);
            
            if (connected)
            {
                IsConnected = true;
                AppendToLog(LocalizationHelper.GetString("connected"));
                
                // Set connection settings for monitoring service (needed for sudo commands)
                _monitoringService.SetConnectionSettings(CurrentConnection);
                
                // Set server base path for discovery service
                string basePath = CurrentConnection.ServerBasePath;
                if (string.IsNullOrEmpty(basePath) && !string.IsNullOrEmpty(CurrentConnection.Username))
                {
                    basePath = $"/home/{CurrentConnection.Username}/asa_server";
                }
                if (!string.IsNullOrEmpty(basePath))
                {
                    _discoveryService.SetBasePath(basePath);
                }
                
                // Initialize chart series now that we're connected
                ChartViewModel.InitializeSeries();
                
                // Auto-discover servers
                await DiscoverServersAsync();
                
                // Start monitoring
                _monitoringService.Start();
            }
            else
            {
                AppendToLog(LocalizationHelper.GetString("error") + ": " + LocalizationHelper.GetString("connection_failed"));
            }
        }
        catch (Exception ex)
        {
            AppendToLog($"{LocalizationHelper.GetString("error")}: {ex.Message}");
        }
    }

    private void Disconnect()
    {
        _monitoringService.Stop();
        _sshService.Disconnect();
        IsConnected = false;
        AppendToLog(LocalizationHelper.GetString("disconnected"));
    }

    private async Task DiscoverServersAsync()
    {
        try
        {
            AppendToLog(LocalizationHelper.GetString("discovering_servers"));
            var discoveredServers = await _discoveryService.DiscoverServersAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Servers.Clear();
                foreach (var server in discoveredServers)
                {
                    var viewModel = new ServerCardViewModel(server, _sshService, _monitoringService, _currentConnection);
                    Servers.Add(viewModel);
                }
            });

            if (discoveredServers.Count == 0)
            {
                AppendToLog(LocalizationHelper.GetString("no_servers_found"));
            }
            else
            {
                AppendToLog($"{discoveredServers.Count} {LocalizationHelper.GetString("servers_discovered")}");
            }
        }
        catch (Exception ex)
        {
            AppendToLog($"{LocalizationHelper.GetString("error")}: {ex.Message}");
        }
    }

    private async Task RefreshAsync()
    {
        if (!IsConnected)
            return;

        try
        {
            AppendToLog(LocalizationHelper.GetString("refreshing"));
            
            // Újrafelderítjük a szervereket
            await DiscoverServersAsync();
            
            // Újraindítjuk a monitoring-ot, hogy az új szervereket is figyelje
            _monitoringService.Stop();
            _monitoringService.Start();
            
            AppendToLog(LocalizationHelper.GetString("refresh_completed"));
        }
        catch (Exception ex)
        {
            AppendToLog($"{LocalizationHelper.GetString("error")}: {ex.Message}");
        }
    }

    private async Task ExecuteClusterActionAsync(string action)
    {
        if (!IsConnected || Servers.Count == 0)
            return;

        AppendToLog($"{LocalizationHelper.GetString(action + "_all")}...");

        foreach (var server in Servers)
        {
            try
            {
                string command = $"cd {server.Model.DirectoryPath} && ./POK-manager.sh -{action} {server.Model.Name}";
                await _sshService.ExecuteCommandAsync(command);
                AppendToLog($"{server.Model.Name}: {LocalizationHelper.GetString(action)} {LocalizationHelper.GetString("success")}");
                
                // 20 second delay between commands
                await Task.Delay(TimeSpan.FromSeconds(20));
            }
            catch (Exception ex)
            {
                AppendToLog($"{server.Model.Name}: {LocalizationHelper.GetString("error")} - {ex.Message}");
            }
        }

        AppendToLog($"{LocalizationHelper.GetString(action + "_all")} {LocalizationHelper.GetString("completed")}");
    }

    private void OpenSettings()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var settingsWindow = new Views.ConnectionSettingsWindow();
            var viewModel = new ConnectionSettingsViewModel(_settingsService);
            settingsWindow.DataContext = viewModel;
            settingsWindow.ShowDialog();

            // Reload connection settings
            CurrentConnection = _settingsService.LoadConnectionSettings();
        });
    }

    private void OpenClusterManagement()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            string basePath = CurrentConnection?.ServerBasePath ?? string.Empty;
            if (string.IsNullOrEmpty(basePath) && !string.IsNullOrEmpty(CurrentConnection?.Username))
            {
                basePath = $"/home/{CurrentConnection.Username}/asa_server";
            }
            var clusterWindow = new Views.ClusterManagementWindow(_sshService, CurrentConnection?.Username ?? string.Empty, IsConnected, basePath);
            clusterWindow.Owner = System.Windows.Application.Current.MainWindow;
            clusterWindow.ShowDialog();
        });
    }

    private void LoadSettings()
    {
        CurrentConnection = _settingsService.LoadConnectionSettings();
        var savedServers = _settingsService.LoadServers();
        
        // Optionally load saved servers if auto-discovery fails
    }

    private Task AddServerAsync()
    {
        if (!IsConnected)
            return Task.CompletedTask;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            string basePath = CurrentConnection?.ServerBasePath ?? string.Empty;
            if (string.IsNullOrEmpty(basePath) && !string.IsNullOrEmpty(CurrentConnection?.Username))
            {
                basePath = $"/home/{CurrentConnection.Username}/asa_server";
            }
            var addServerWindow = new Views.AddServerWindow(_sshService, CurrentConnection?.Username ?? string.Empty, basePath);
            addServerWindow.Owner = System.Windows.Application.Current.MainWindow;
            var result = addServerWindow.ShowDialog();

            if (result == true)
            {
                // Refresh server list after adding
                _ = DiscoverServersAsync();
            }
        });

        return Task.CompletedTask;
    }

    private async Task AddServerByPathAsync(string directoryPath)
    {
        try
        {
            // Validate directory exists and contains POK-manager.sh
            string checkCommand = $"test -d {directoryPath} && test -f {directoryPath}/POK-manager.sh && echo 'valid' || echo 'invalid'";
            string checkOutput = await _sshService.ExecuteCommandAsync(checkCommand);

            if (!checkOutput.Contains("valid"))
            {
                AppendToLog($"{LocalizationHelper.GetString("error")}: {LocalizationHelper.GetString("invalid_directory")}");
                System.Windows.MessageBox.Show(
                    LocalizationHelper.GetString("pok_manager_not_found"),
                    LocalizationHelper.GetString("error"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Extract folder name from path
            string folderName = directoryPath.Contains('/')
                ? directoryPath.Substring(directoryPath.LastIndexOf('/') + 1)
                : directoryPath;

            // Check if server already exists
            if (Servers.Any(s => s.Model.Name == folderName))
            {
                AppendToLog($"{LocalizationHelper.GetString("error")}: {LocalizationHelper.GetString("server_name")} már létezik");
                return;
            }

            // Create server instance
            var server = new ServerInstance
            {
                Name = folderName,
                DirectoryPath = directoryPath,
                MapName = ExtractMapName(folderName),
                Status = ServerStatus.Offline
            };

            // Try to load ports from .env
            await TryLoadPortsFromEnvAsync(server);

            // Add to collection
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var viewModel = new ServerCardViewModel(server, _sshService, _monitoringService, _currentConnection);
                Servers.Add(viewModel);
            });

            // Save to settings
            SaveServersToSettings();

            AppendToLog($"{LocalizationHelper.GetString("server_added")}: {folderName}");
        }
        catch (Exception ex)
        {
            AppendToLog($"{LocalizationHelper.GetString("error")}: {ex.Message}");
        }
    }

    private async Task RemoveServerAsync(ServerCardViewModel? serverViewModel)
    {
        if (serverViewModel == null || !IsConnected)
            return;

        string directoryPath = serverViewModel.Model.DirectoryPath;
        string serverName = serverViewModel.Model.Name;

        // Show warning message with full directory path
        string message = $"{LocalizationHelper.GetString("remove_server_warning")}\n\n" +
                        $"{LocalizationHelper.GetString("server_name")}: {serverName}\n" +
                        $"{LocalizationHelper.GetString("directory_path")}: {directoryPath}\n\n" +
                        $"{LocalizationHelper.GetString("delete_directory_warning")}";

        var result = System.Windows.MessageBox.Show(
            message,
            LocalizationHelper.GetString("remove_server"),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                // Delete directory on server
                string deleteCommand = $"rm -rf \"{directoryPath}\"";
                await _sshService.ExecuteCommandAsync(deleteCommand);

                // Unregister from monitoring
                _monitoringService.UnregisterServer(serverName);

                // Remove from collection
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Servers.Remove(serverViewModel);
                });

                // Save to settings
                SaveServersToSettings();

                AppendToLog($"{LocalizationHelper.GetString("server_removed")}: {serverName}");
                AppendToLog($"{LocalizationHelper.GetString("directory_deleted")}: {directoryPath}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"{LocalizationHelper.GetString("delete_directory_error")}: {ex.Message}",
                    LocalizationHelper.GetString("error"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                AppendToLog($"{LocalizationHelper.GetString("delete_directory_error")}: {ex.Message}");
            }
        }
    }

    private string ExtractMapName(string folderName)
    {
        // Extract map name from folder name
        var match = System.Text.RegularExpressions.Regex.Match(folderName, @"(?:Rexodon-|.*-)(\w+)");
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value;
        }
        return folderName;
    }

    private async Task TryLoadPortsFromEnvAsync(ServerInstance server)
    {
        try
        {
            string envPath = $"{server.DirectoryPath}/.env";
            string command = $"cat {envPath} 2>/dev/null";
            string output = await _sshService.ExecuteCommandAsync(command);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("SERVER_PORT=", StringComparison.OrdinalIgnoreCase))
                {
                    var portMatch = System.Text.RegularExpressions.Regex.Match(line, @"SERVER_PORT=(\d+)");
                    if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out int port))
                    {
                        server.AsaPort = port;
                    }
                }
                else if (line.StartsWith("RCON_PORT=", StringComparison.OrdinalIgnoreCase))
                {
                    var portMatch = System.Text.RegularExpressions.Regex.Match(line, @"RCON_PORT=(\d+)");
                    if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out int port))
                    {
                        server.RconPort = port;
                    }
                }
            }
        }
        catch
        {
            // .env file might not exist or be readable, use defaults
            server.AsaPort = 0;
            server.RconPort = 0;
        }
    }

    private void SaveServersToSettings()
    {
        var serverList = Servers.Select(s => s.Model).ToList();
        _settingsService.SaveServers(serverList);
    }

    private void OnSshOutputReceived(object? sender, string output)
    {
        AppendToLog(output);
    }

    private void OnSshErrorReceived(object? sender, string error)
    {
        AppendToLog($"ERROR: {error}");
    }

    private void OnStatusOutputReceived(object? sender, string message)
    {
        AppendToLog(message);
    }

    private void OnSshConnectionLost(object? sender, EventArgs e)
    {
        try
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsConnected = false;
                    AppendToLog(LocalizationHelper.GetString("connection_lost"));
                    
                    // Show reconnect dialog
                    var result = System.Windows.MessageBox.Show(
                        LocalizationHelper.GetString("connection_lost") + ". " + LocalizationHelper.GetString("reconnect") + "?",
                        LocalizationHelper.GetString("error"),
                        System.Windows.MessageBoxButton.YesNo);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        _ = ConnectAsync();
                    }
                });
            }
            else
            {
                IsConnected = false;
                AppendToLog(LocalizationHelper.GetString("connection_lost"));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Connection lost handler hiba: {ex.Message}");
        }
    }

    private void OnServerStatsUpdated(object? sender, MonitoringService.ServerStatsEventArgs e)
    {
        // Stats are updated in ServerCardViewModel
        
        // Update chart with aggregated data from all servers
        // Frissítjük a chart-ot az összes szerver összesített adatával
        if (Servers.Count > 0 && ChartViewModel != null)
        {
            try
            {
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // Összesítjük az összes szerver CPU használatát
                            // A CPU használat az összes szerver CPU használatának összege
                            // (minden szerver CPU használata már a teljes rendszer százalékában van)
                            double totalCpuUsage = 0;
                            double totalMemoryPercent = 0;
                            double totalDiskUsagePercent = 0;
                            double totalNetworkRxMbps = 0;
                            double totalNetworkTxMbps = 0;
                            int onlineServerCount = 0;

                            // Rendszer szintű CPU és RAM használat (mint Btop-ban)
                            // Ezek minden szerverre ugyanazok, csak egyszer kell lekérni
                            var firstServerStats = _monitoringService.GetServerStats(e.ServerName);
                            if (firstServerStats != null)
                            {
                                // Rendszer szintű CPU és RAM használat (mint Btop-ban)
                                totalCpuUsage = firstServerStats.SystemCpuUsage;
                                totalMemoryPercent = firstServerStats.SystemMemoryUsagePercent;
                                totalDiskUsagePercent = firstServerStats.DiskUsagePercent;
                                totalNetworkRxMbps = firstServerStats.NetworkRxMbps;
                                totalNetworkTxMbps = firstServerStats.NetworkTxMbps;
                                onlineServerCount = 1; // Rendszer szintű adatok esetén mindig van adat
                            }

                            // Ha van online szerver, akkor frissítjük a chart-ot
                            // Vagy ha nincs online szerver, de van rendszer szintű adat, akkor is frissítjük
                            if (onlineServerCount > 0 || totalDiskUsagePercent > 0)
                            {
                                // CPU használat: rendszer szintű CPU használat (mint Btop-ban)
                                double aggregatedCpuUsage = totalCpuUsage;
                                
                                // Memory: rendszer szintű RAM használat (mint Btop-ban)
                                double aggregatedMemoryPercent = totalMemoryPercent;
                                
                                // Disk: rendszer szintű (mindig ugyanaz)
                                double aggregatedDiskUsagePercent = totalDiskUsagePercent;
                                
                                // Network: rendszer szintű (mindig ugyanaz)
                                double aggregatedNetworkRxMbps = totalNetworkRxMbps;
                                double aggregatedNetworkTxMbps = totalNetworkTxMbps;

                                System.Diagnostics.Debug.WriteLine($"Chart update: CPU={aggregatedCpuUsage:F2}%, Memory={aggregatedMemoryPercent:F2}%, Disk={aggregatedDiskUsagePercent:F2}%, NetworkRx={aggregatedNetworkRxMbps:F2}Mbps, NetworkTx={aggregatedNetworkTxMbps:F2}Mbps, OnlineServers={onlineServerCount}");

                                // AddDataPoint expects double for memory, not string
                                ChartViewModel.AddDataPoint(
                                    aggregatedCpuUsage,
                                    aggregatedMemoryPercent, // Already a double (percentage)
                                    aggregatedDiskUsagePercent,
                                    aggregatedNetworkRxMbps,
                                    aggregatedNetworkTxMbps);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Chart update skipped: onlineServerCount={onlineServerCount}, totalDiskUsagePercent={totalDiskUsagePercent}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Chart frissítési hiba: {ex.Message}");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnServerStatsUpdated hiba: {ex.Message}");
            }
        }
    }

    private double ParseMemoryPercentage(string memoryUsage)
    {
        // Parse string like "2.5GiB / 8GiB" to percentage
        try
        {
            if (string.IsNullOrEmpty(memoryUsage))
                return 0;

            var parts = memoryUsage.Split('/');
            if (parts.Length == 2)
            {
                double used = ParseSize(parts[0].Trim());
                double total = ParseSize(parts[1].Trim());
                
                if (total > 0)
                {
                    return (used / total) * 100.0;
                }
            }
        }
        catch
        {
            // If parsing fails, return 0
        }
        
        return 0;
    }

    private double ParseSize(string size)
    {
        // Parse size like "2.5GiB", "512MiB", etc.
        size = size.Trim();
        if (string.IsNullOrEmpty(size))
            return 0;

        double multiplier = 1;
        if (size.EndsWith("GiB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024 * 1024 * 1024; // GB to bytes
            size = size.Substring(0, size.Length - 3).Trim();
        }
        else if (size.EndsWith("MiB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024 * 1024; // MB to bytes
            size = size.Substring(0, size.Length - 3).Trim();
        }
        else if (size.EndsWith("KiB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024; // KB to bytes
            size = size.Substring(0, size.Length - 3).Trim();
        }

        if (double.TryParse(size, out double value))
        {
            return value * multiplier;
        }

        return 0;
    }

    private void AppendToLog(string message)
    {
        try
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    GlobalLog += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
                    OnPropertyChanged(nameof(GlobalLog));
                });
            }
            else
            {
                GlobalLog += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
                OnPropertyChanged(nameof(GlobalLog));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Log hozzáadási hiba: {ex.Message}");
        }
    }

}
