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
    private readonly NotificationService _notificationService;
    private LoggingService? _loggingService;
    private AdminService? _adminService;
    private LicenseService? _licenseService;
    private LicenseValidator? _licenseValidator;
    private DatabaseService? _databaseService;

    private ConnectionSettings? _currentConnection;
    private bool _isConnected;
    private string _globalLog = string.Empty;
    private bool _isLogExpanded;
    private ChartViewModel? _chartViewModel;
    private string _currentUsername = string.Empty;
    private SavedServer? _selectedSavedServer;
    private ObservableCollection<SavedServer> _savedServers = new();
    private string _updateButtonText = "Frissítés";
    private bool _isManagerAdmin = false;
    private Models.User? _currentUser;
    
    // Navigation
    private ObservableCollection<NavigationItem> _navigationItems = new();
    private NavigationItem? _selectedNavigationItem;
    private object? _selectedView;

    public MainViewModel(string username, Models.User? user = null)
    {
        _currentUsername = username;
        _currentUser = user;
        
        // Check if user is Manager Admin
        if (user != null)
        {
            IsManagerAdmin = user.UserType == UserType.ManagerAdmin;
        }
        _sshService = new SshService();
        _settingsService = new SettingsService();
        _userService = new UserService();
        _serverDataService = new ServerDataService();
        _discoveryService = new ServerDiscoveryService(_sshService);
        _monitoringService = new MonitoringService(_sshService);
        _updateService = new UpdateService();
        _notificationService = new NotificationService();
        _notificationService = new NotificationService();

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
        OpenChangelogCommand = new RelayCommandSync(() => OpenChangelog());
        OpenNotificationsCommand = new RelayCommandSync(() => OpenNotifications());
        OpenAdminManagementCommand = new RelayCommandSync(() => OpenAdminManagement());
        OpenManagerAdminCommand = new RelayCommandSync(() => OpenManagerAdmin());
        OpenUserProfileCommand = new RelayCommandSync(() => OpenUserProfile());
        ViewTermsCommand = new RelayCommandSync(() => ViewTerms());
        _sshService.OutputReceived += OnSshOutputReceived;
        _sshService.ErrorReceived += OnSshErrorReceived;
        _sshService.ConnectionLost += OnSshConnectionLost;
        _monitoringService.ServerStatsUpdated += OnServerStatsUpdated;
        _monitoringService.StatusOutputReceived += OnStatusOutputReceived;
        _monitoringService.ServerStatusChanged += OnServerStatusChanged;
        _notificationService.NotificationAdded += OnNotificationAdded;

        ChartViewModel = new ChartViewModel();

        LoadSavedServers();
        LoadUpdateButtonText();
        InitializeNavigation();
    }
    
    private void InitializeNavigation()
    {
        _navigationItems.Clear();
        
        // Dashboard - Always try to create database service if user is logged in
        DatabaseService? dashboardDbService = null;
        if (_currentUser != null)
        {
            try
            {
                dashboardDbService = new DatabaseService(DatabaseConfiguration.GetConnectionString());
                // Store it for later use
                if (_databaseService == null)
                {
                    _databaseService = dashboardDbService;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize database service in InitializeNavigation: {ex.Message}");
                // If database connection fails, dashboard will work without news
            }
        }
        
        var dashboardViewModel = new DashboardViewModel(dashboardDbService);
        var dashboardItem = new NavigationItem
        {
            Title = LocalizationHelper.GetString("dashboard") ?? "Dashboard",
            Icon = "🏠",
            View = new Views.DashboardView { DataContext = dashboardViewModel }
        };
        _navigationItems.Add(dashboardItem);
        
        // Servers
        var serversItem = new NavigationItem
        {
            Title = LocalizationHelper.GetString("servers") ?? "Servers",
            Icon = "🖥️",
            View = new Views.ServersView { DataContext = this }
        };
        _navigationItems.Add(serversItem);
        
        // Performance Data
        // Ensure ChartViewModel is initialized
        if (ChartViewModel != null)
        {
            ChartViewModel.InitializeSeries();
        }
        var performanceDataViewModel = new PerformanceDataViewModel(_monitoringService, ChartViewModel);
        var performanceDataItem = new NavigationItem
        {
            Title = LocalizationHelper.GetString("performance_data") ?? "Performance Data",
            Icon = "📊",
            View = new Views.PerformanceDataView { DataContext = performanceDataViewModel }
        };
        _navigationItems.Add(performanceDataItem);
        
        // Administration
        var adminItem = new NavigationItem
        {
            Title = LocalizationHelper.GetString("administration") ?? "Administration",
            Icon = "⚙️",
            View = new Views.AdministrationView { DataContext = this }
        };
        _navigationItems.Add(adminItem);
        
        // Settings
        var settingsItem = new NavigationItem
        {
            Title = LocalizationHelper.GetString("settings") ?? "Settings",
            Icon = "🔧",
            View = new Views.SettingsView { DataContext = this }
        };
        _navigationItems.Add(settingsItem);
        
        // Help
        var helpItem = new NavigationItem
        {
            Title = LocalizationHelper.GetString("help") ?? "Help",
            Icon = "❓",
            View = new Views.HelpView { DataContext = this }
        };
        _navigationItems.Add(helpItem);
        
        // Set default selection
        SelectedNavigationItem = dashboardItem;
    }

    private void LoadUpdateButtonText()
    {
        UpdateButtonText = Utilities.LocalizationHelper.GetString("manager_update");
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

    private bool _canAddServer = true;
    private bool _canRemoveServer = true;
    private bool _canClusterManagement = true;

    public bool CanAddServer { get => _canAddServer; private set => SetProperty(ref _canAddServer, value); }
    public bool CanRemoveServer { get => _canRemoveServer; private set => SetProperty(ref _canRemoveServer, value); }
    public bool CanClusterManagement { get => _canClusterManagement; private set => SetProperty(ref _canClusterManagement, value); }

    private async Task LoadPermissionsAsync()
    {
        // If user is Manager Admin or Server Admin, grant all permissions
        if (_currentUser != null && (_currentUser.UserType == UserType.ManagerAdmin || _currentUser.UserType == UserType.ServerAdmin))
        {
            CanAddServer = true;
            CanRemoveServer = true;
            CanClusterManagement = true;
            return;
        }

        if (_adminService == null || string.IsNullOrEmpty(_currentUsername))
            return;

        try
        {
            CanAddServer = await _adminService.HasPermissionAsync(_currentUsername, "AddServer");
            CanRemoveServer = await _adminService.HasPermissionAsync(_currentUsername, "RemoveServer");
            CanClusterManagement = await _adminService.HasPermissionAsync(_currentUsername, "ClusterManagement");
        }
        catch
        {
            // Default to allowing on error
        }
    }

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
    public ICommand OpenChangelogCommand { get; }
    public ICommand OpenAdminManagementCommand { get; }

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

    public int NotificationUnreadCount => _notificationService.GetUnreadCount();
    public bool HasUnreadNotifications => NotificationUnreadCount > 0;

    public bool IsManagerAdmin
    {
        get => _isManagerAdmin;
        set => SetProperty(ref _isManagerAdmin, value);
    }

    public ICommand OpenNotificationsCommand { get; }
    public ICommand OpenManagerAdminCommand { get; }
    public ICommand OpenUserProfileCommand { get; }
    public ICommand ViewTermsCommand { get; }
    
    // Navigation Properties
    public ObservableCollection<NavigationItem> NavigationItems
    {
        get => _navigationItems;
        set => SetProperty(ref _navigationItems, value);
    }
    
    public NavigationItem? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (SetProperty(ref _selectedNavigationItem, value))
            {
                SelectedView = value?.View;
                
                // If Dashboard is selected, try to refresh news if database service is available
                if (value?.View is Views.DashboardView dashboardView && 
                    dashboardView.DataContext is DashboardViewModel dashboardVM)
                {
                    // Try to update with database service if it wasn't available before
                    if (_databaseService != null)
                    {
                        dashboardVM.SetDatabaseService(_databaseService);
                        // Also reload news
                        _ = dashboardVM.LoadNewsAsync();
                    }
                }
            }
        }
    }
    
    public object? SelectedView
    {
        get => _selectedView;
        set => SetProperty(ref _selectedView, value);
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
                
                // Automatikusan csatlakozunk az új szerverhez
                _ = ConnectAsync();
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
                
                // Initialize logging service
                _loggingService = new LoggingService(_sshService, CurrentConnection);
                await _loggingService.InitializeAsync();
                await _loggingService.LogConnectionAsync(_currentUsername, CurrentConnection.Host, true);
                
                // Initialize admin service
                _adminService = new AdminService(_sshService, CurrentConnection);
                await _adminService.InitializeAsync();
                await _adminService.EnsureFirstUserIsAdminAsync(_currentUsername);
                
                // Initialize database service and license service
                if (_currentUser != null)
                {
                    try
                    {
                        if (_databaseService == null)
                        {
                            _databaseService = new DatabaseService(DatabaseConfiguration.GetConnectionString());
                        }
                        var licenseRepository = new Services.Repository.LicenseRepository(_databaseService);
                        var serverRepository = new Services.Repository.ServerRepository(_databaseService);
                        var userRepository = new Services.Repository.UserRepository(_databaseService);
                        _licenseService = new LicenseService(licenseRepository, serverRepository, userRepository);
                        
                        // Validate license
                        var licenseValidation = await _licenseService.ValidateLicenseAsync(_currentUser.Id);
                        if (!licenseValidation.IsValid)
                        {
                            System.Windows.MessageBox.Show(
                                $"License validation failed: {licenseValidation.ErrorMessage}",
                                "License Error",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        }
                        
                        // Start license validator
                        _licenseValidator = new LicenseValidator(licenseRepository, _licenseService);
                        _licenseValidator.LicenseValidationFailed += OnLicenseValidationFailed;
                        _licenseValidator.Start(_currentUser.Id);
                        
                        // Update DashboardViewModel with database service if it was null before
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Find DashboardView in navigation items
                            foreach (var navItem in _navigationItems)
                            {
                                if (navItem.View is Views.DashboardView dashboardView)
                                {
                                    if (dashboardView.DataContext is DashboardViewModel oldVM)
                                    {
                                        // Try to set database service if it wasn't available before
                                        oldVM.SetDatabaseService(_databaseService);
                                        // Also reload news
                                        _ = oldVM.LoadNewsAsync();
                                        break;
                                    }
                                }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to initialize license service: {ex.Message}");
                    }
                }
                
                // Load permissions
                await LoadPermissionsAsync();
                
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
        if (_currentConnection != null && _loggingService != null)
        {
            _ = _loggingService.LogConnectionAsync(_currentUsername, _currentConnection.Host, false);
        }
        _sshService.Disconnect();
        IsConnected = false;
        AppendToLog(LocalizationHelper.GetString("disconnected"));
    }

    private async Task<List<ServerInstance>> DiscoverServersAsync()
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
                    var viewModel = new ServerCardViewModel(server, _sshService, _monitoringService, _currentConnection, _loggingService, _currentUsername, _adminService, _notificationService, _currentUser);
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

            return discoveredServers;
        }
        catch (Exception ex)
        {
            AppendToLog($"{LocalizationHelper.GetString("error")}: {ex.Message}");
            return new List<ServerInstance>();
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
            var servers = await DiscoverServersAsync();
            
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
                
                if (_loggingService != null)
                {
                    await _loggingService.LogServerActionAsync(_currentUsername, LocalizationHelper.GetString(action.Replace("_all", "")), server.Model.Name);
                }
                
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
            var viewModel = new ConnectionSettingsViewModel(_settingsService, _serverDataService, _currentUsername, SavedServers, SelectedSavedServer, false);
            settingsWindow.DataContext = viewModel;
            var result = settingsWindow.ShowDialog();

            if (result == true)
            {
                // Frissítjük a SavedServers listát
                LoadSavedServers();
                
                // Ha van kiválasztott szerver, betöltjük a kapcsolatot
                if (viewModel.SelectedSavedServer != null)
                {
                    SelectedSavedServer = viewModel.SelectedSavedServer;
                    LoadServerConnection(SelectedSavedServer);
                    
                    // Ha új szerver lett hozzáadva vagy váltottunk szervert, automatikusan csatlakozunk
                    if (IsConnected)
                    {
                        // Ha már csatlakozva vagyunk, újracsatlakozunk az új adatokkal
                        _ = Task.Run(async () =>
                        {
                            Disconnect();
                            await Task.Delay(500);
                            await ConnectAsync();
                        });
                    }
                    else
                    {
                        // Ha nincs csatlakozás, csatlakozunk
                        _ = ConnectAsync();
                    }
                }
            }
        });
    }

    private async void OpenClusterManagement()
    {
        // Check permission
        if (_adminService != null && !string.IsNullOrEmpty(_currentUsername))
        {
            bool hasPermission = await _adminService.HasPermissionAsync(_currentUsername, "ClusterManagement");
            if (!hasPermission)
            {
                System.Windows.MessageBox.Show(
                    "You do not have permission to perform this action.",
                    LocalizationHelper.GetString("error"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }
        }

        // Check license limits for cluster creation
        if (_licenseService != null && _currentUser != null)
        {
            // Count existing clusters from database
            if (_databaseService != null)
            {
                var serverRepository = new Services.Repository.ServerRepository(_databaseService);
                var servers = await serverRepository.GetByUserIdAsync(_currentUser.Id);
                var existingClusters = servers.Select(s => s.ClusterName).Where(c => !string.IsNullOrEmpty(c)).Distinct().Count();
                
                var licenseCheck = await _licenseService.CheckLicenseLimitsAsync(_currentUser.Id, clustersToAdd: 1);
                if (!licenseCheck.IsValid)
                {
                    System.Windows.MessageBox.Show(
                        licenseCheck.ErrorMessage ?? "License limit exceeded.",
                        "License Limit",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }
        }

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

    private void OpenChangelog()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var changelogWindow = new Views.ChangelogWindow();
            changelogWindow.Owner = System.Windows.Application.Current.MainWindow;
            changelogWindow.ShowDialog();
        });
    }

    private void LoadSettings()
    {
        CurrentConnection = _settingsService.LoadConnectionSettings();
        var savedServers = _settingsService.LoadServers();
        
        // Optionally load saved servers if auto-discovery fails
    }

    private async Task AddServerAsync()
    {
        if (!IsConnected)
            return;

        // Check permission - ServerAdmin and ManagerAdmin have all permissions
        if (_currentUser != null && (_currentUser.UserType == UserType.ManagerAdmin || _currentUser.UserType == UserType.ServerAdmin))
        {
            // ServerAdmin and ManagerAdmin have permission, continue
        }
        else if (_adminService != null && !string.IsNullOrEmpty(_currentUsername))
        {
            bool hasPermission = await _adminService.HasPermissionAsync(_currentUsername, "AddServer");
            if (!hasPermission)
            {
                System.Windows.MessageBox.Show(
                    "You do not have permission to perform this action.",
                    LocalizationHelper.GetString("error"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }
        }

        // Check license limits
        if (_licenseService != null && _currentUser != null)
        {
            var licenseCheck = await _licenseService.CheckLicenseLimitsAsync(_currentUser.Id, clustersToAdd: 0, serversToAdd: 1);
            if (!licenseCheck.IsValid)
            {
                System.Windows.MessageBox.Show(
                    licenseCheck.ErrorMessage ?? "License limit exceeded.",
                    "License Limit",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }
        }

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
                _ = Task.Run(async () => await DiscoverServersAsync());
            }
        });
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
                var viewModel = new ServerCardViewModel(server, _sshService, _monitoringService, _currentConnection, _loggingService, _currentUsername, _adminService, _notificationService, _currentUser);
                Servers.Add(viewModel);
            });

            // Save to settings
            SaveServersToSettings();

            if (_loggingService != null)
            {
                await _loggingService.LogServerActionAsync(_currentUsername, LocalizationHelper.GetString("add_server"), folderName);
            }

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

        // Check permission - ServerAdmin and ManagerAdmin have all permissions
        if (_currentUser != null && (_currentUser.UserType == UserType.ManagerAdmin || _currentUser.UserType == UserType.ServerAdmin))
        {
            // ServerAdmin and ManagerAdmin have permission, continue
        }
        else if (_adminService != null && !string.IsNullOrEmpty(_currentUsername))
        {
            bool hasPermission = await _adminService.HasPermissionAsync(_currentUsername, "RemoveServer");
            if (!hasPermission)
            {
                System.Windows.MessageBox.Show(
                    "You do not have permission to perform this action.",
                    LocalizationHelper.GetString("error"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }
        }

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

                if (_loggingService != null)
                {
                    await _loggingService.LogServerActionAsync(_currentUsername, LocalizationHelper.GetString("remove_server"), serverName);
                }

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

    private void OnServerStatusChanged(object? sender, MonitoringService.ServerStatusChangedEventArgs e)
    {
        try
        {
            if (e.OldStatus == Models.ServerStatus.Offline && e.NewStatus == Models.ServerStatus.Online)
            {
                _notificationService.ShowNotification(NotificationService.NotificationType.ServerStarted, e.ServerName);
            }
            else if (e.OldStatus == Models.ServerStatus.Online && e.NewStatus == Models.ServerStatus.Offline)
            {
                // Check if this was a scheduled shutdown completion
                if (e.IsShutdownCompletion)
                {
                    _notificationService.ShowNotification(NotificationService.NotificationType.ShutdownCompleted, e.ServerName);
                }
                else
                {
                    _notificationService.ShowNotification(NotificationService.NotificationType.ServerStopped, e.ServerName);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification error: {ex.Message}");
        }
    }

    public NotificationService NotificationService => _notificationService;

    private void OnNotificationAdded(object? sender, NotificationService.NotificationItem notification)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(NotificationUnreadCount));
            OnPropertyChanged(nameof(HasUnreadNotifications));
        });
    }

    private void OpenNotifications()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var notificationWindow = new Views.NotificationWindow(_notificationService);
            notificationWindow.Owner = System.Windows.Application.Current.MainWindow;
            notificationWindow.ShowDialog();
            
            // Refresh unread count after closing
            OnPropertyChanged(nameof(NotificationUnreadCount));
            OnPropertyChanged(nameof(HasUnreadNotifications));
        });
    }

    private void OpenManagerAdmin()
    {
        try
        {
            if (_databaseService == null)
            {
                _databaseService = new DatabaseService(DatabaseConfiguration.GetConnectionString());
            }
            var viewModel = new ManagerAdminViewModel(_databaseService, _currentUser?.Id ?? Guid.Empty);
            var window = new Views.ManagerAdminWindow(viewModel);
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error opening Manager Admin: {ex.Message}",
                LocalizationHelper.GetString("error"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OpenUserProfile()
    {
        if (_currentUser == null || _databaseService == null)
        {
            System.Windows.MessageBox.Show(
                LocalizationHelper.GetString("user_not_loaded") ?? "User information not available.",
                LocalizationHelper.GetString("error") ?? "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var profileWindow = new Views.UserProfileWindow(_currentUser, _databaseService);
            profileWindow.Owner = System.Windows.Application.Current.MainWindow;
            profileWindow.ShowDialog();
        });
    }
    
    private void ViewTerms()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var termsWindow = new Views.TermsWindow();
            termsWindow.Owner = System.Windows.Application.Current.MainWindow;
            termsWindow.ShowDialog();
        });
    }
    
    private void OnLicenseValidationFailed(object? sender, LicenseValidationEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show(
                $"License validation failed: {e.ErrorMessage}",
                "License Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        });
    }

    private async void OpenAdminManagement()
    {
        if (_adminService == null || !IsConnected)
        {
            System.Windows.MessageBox.Show(
                LocalizationHelper.GetString("disconnected"),
                LocalizationHelper.GetString("error"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        // Check if current user is server admin or manager admin
        // ServerAdmin and ManagerAdmin can manage other admins
        bool isServerAdmin = _currentUser?.UserType == UserType.ServerAdmin;
        bool isManagerAdmin = _currentUser?.UserType == UserType.ManagerAdmin;
        if (!isServerAdmin && !isManagerAdmin)
        {
            System.Windows.MessageBox.Show(
                "Only server admins and manager admins can manage other admins.",
                LocalizationHelper.GetString("error"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var viewModel = new AdminManagementViewModel(_adminService, _loggingService, _currentUsername);
            var adminWindow = new Views.AdminManagementWindow(viewModel);
            adminWindow.Owner = System.Windows.Application.Current.MainWindow;
            adminWindow.ShowDialog();
        });
    }

}
