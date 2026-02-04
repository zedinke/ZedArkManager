using System.Windows.Input;
using ZedASAManager.Models;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.ViewModels;

public class ServerCardViewModel : ViewModelBase
{
    private readonly SshService _sshService;
    private readonly MonitoringService _monitoringService;
    private readonly ConnectionSettings? _connectionSettings;
    private ServerInstance _model;
    private bool _isBusy;

    public ServerCardViewModel(ServerInstance model, SshService sshService, MonitoringService monitoringService, ConnectionSettings? connectionSettings = null)
    {
        _model = model;
        _sshService = sshService;
        _monitoringService = monitoringService;
        _connectionSettings = connectionSettings;

        StartCommand = new RelayCommand(async () => await ExecuteActionAsync("start"), () => !IsBusy);
        StopCommand = new RelayCommand(async () => await ExecuteStopAsync(), () => !IsBusy);
        RestartCommand = new RelayCommand(async () => await ExecuteActionAsync("restart"), () => !IsBusy);
        UpdateCommand = new RelayCommand(async () => await ExecuteUpdateAsync(), () => !IsBusy);
        ShutdownCommand = new RelayCommand(async () => await ExecuteShutdownAsync(), () => !IsBusy);
        ConfigCommand = new RelayCommandSync(() => OpenConfigWindow());
        OpenLiveLogsCommand = new RelayCommandSync(() => OpenLiveLogsWindow());
        DockerSetupCommand = new RelayCommand(async () => await OpenDockerSetupWindowAsync());

        _monitoringService.ServerStatsUpdated += OnServerStatsUpdated;
        _monitoringService.RegisterServer(model.Name, model.DirectoryPath);
    }

    public ServerInstance Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RestartCommand).RaiseCanExecuteChanged();
                ((RelayCommand)UpdateCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string MapName => Model.MapName;
    public string ServerName => Model.Name;
    public ServerStatus Status => Model.Status;
    public double CpuUsage => Model.CpuUsage;
    public string MemoryUsage => Model.MemoryUsage;
    public string PortInfo => Model.AsaPort > 0 ? $"{LocalizationHelper.GetString("port_info")}: {Model.AsaPort}" : "";
    public string PlayerInfo => $"{Model.OnlinePlayers}/{Model.MaxPlayers}";
    public string GameDayInfo => Model.GameDay > 0 ? $"{LocalizationHelper.GetString("day")}: {Model.GameDay}" : "";
    public string ServerVersionInfo => !string.IsNullOrEmpty(Model.ServerVersion) ? $"{LocalizationHelper.GetString("version")}: {Model.ServerVersion}" : "";
    public string ServerPingInfo => !string.IsNullOrEmpty(Model.ServerPing) ? $"{LocalizationHelper.GetString("ping")}: {Model.ServerPing}" : "";

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand ShutdownCommand { get; }
    public ICommand ConfigCommand { get; }
    public ICommand OpenLiveLogsCommand { get; }
    public ICommand DockerSetupCommand { get; }

    private async Task<string> GetInstanceNameAsync()
    {
        // Extract instance name from Instance_* directories (same logic as MonitoringService)
        string instanceName = string.Empty;
        
        try
        {
            // Find Instance_* directories in the server directory
            string findInstanceCommand = $"find \"{Model.DirectoryPath}\" -maxdepth 1 -type d -name 'Instance_*' 2>/dev/null | head -1";
            string foundInstancePath = await _sshService.ExecuteCommandAsync(findInstanceCommand);
            
            if (!string.IsNullOrEmpty(foundInstancePath.Trim()))
            {
                // Extract instance name from path like: /path/to/Instance_aberrationteszt
                string instancePath = foundInstancePath.Trim();
                int lastSlash = instancePath.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < instancePath.Length - 1)
                {
                    string instanceDirName = instancePath.Substring(lastSlash + 1);
                    if (instanceDirName.StartsWith("Instance_"))
                    {
                        instanceName = instanceDirName.Substring("Instance_".Length);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error finding instance directory: {ex.Message}");
        }

        // Fallback: if we couldn't find Instance_* directory, try to extract from server name
        if (string.IsNullOrEmpty(instanceName))
        {
            if (Model.Name.Contains('_'))
            {
                string[] parts = Model.Name.Split('_');
                if (parts.Length > 1)
                {
                    // Take the last part as instance name
                    instanceName = parts[parts.Length - 1];
                }
            }
            else
            {
                instanceName = Model.Name;
            }
        }

        return instanceName;
    }

    private async Task ExecuteActionAsync(string action)
    {
        if (IsBusy || !_sshService.IsConnected)
            return;

        // Get instance name (without Instance_ prefix) - needed for confirmation dialog
        string instanceName = await GetInstanceNameAsync();

        // Show confirmation dialog for start action
        if (action == "start")
        {
            // Check for xaudio2_9.dll file before starting
            var xAudioService = new XAudioFileService(_sshService);
            bool fileExists = await xAudioService.CheckFileExistsAsync(Model.DirectoryPath);
            
            string pokManagerPath = $"{Model.DirectoryPath}/POK-manager.sh";
            string fullCommand = $"cd {Model.DirectoryPath} && ./POK-manager.sh -start {instanceName}";
            string message = $"{LocalizationHelper.GetString("start_server_confirm")}\n\n" +
                           $"{LocalizationHelper.GetString("pok_manager_path")}: {pokManagerPath}\n\n" +
                           $"Parancs: {fullCommand}";
            
            if (!fileExists)
            {
                message += "\n\n⚠ WARNING: xaudio2_9.dll file is missing from the server directory.\n" +
                           "Missing this file may cause continuous server restarts.\n" +
                           "The file will be automatically downloaded before starting the server.";
            }
            
            var result = System.Windows.MessageBox.Show(
                message,
                LocalizationHelper.GetString("start"),
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            
            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            // Download file if missing
            if (!fileExists)
            {
                IsBusy = true;
                Model.Status = ServerStatus.Busy;
                OnPropertyChanged(nameof(Status));
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show(
                        "Downloading xaudio2_9.dll file. Please wait...",
                        "Downloading",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                });

                bool downloadSuccess = await xAudioService.DownloadAndCopyFileAsync(Model.DirectoryPath);
                
                if (!downloadSuccess)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show(
                            "Failed to download xaudio2_9.dll file. The server may not start correctly.\n" +
                            "Please check your internet connection and try again.",
                            "Download Failed",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    });
                    
                    IsBusy = false;
                    Model.Status = ServerStatus.Offline;
                    OnPropertyChanged(nameof(Status));
                    return;
                }
            }
        }

        IsBusy = true;
        Model.Status = ServerStatus.Busy;
        OnPropertyChanged(nameof(Status));

        try
        {
            // Use the exact same format as MainViewModel which works
            // For start and restart, add yes | for non-interactive mode and handle sudo password
            string command;
            if (action == "start" || action == "restart")
            {
                // Get sudo password if available (assume SSH password is same as sudo password)
                string sudoPasswordPart = string.Empty;
                if (_connectionSettings != null && !_connectionSettings.UseSshKey && !string.IsNullOrEmpty(_connectionSettings.EncryptedPassword))
                {
                    try
                    {
                        string password = EncryptionService.Decrypt(_connectionSettings.EncryptedPassword);
                        if (!string.IsNullOrEmpty(password))
                        {
                            // Escape password for shell (replace single quotes with '\'' and wrap in single quotes)
                            string escapedPassword = password.Replace("'", "'\\''");
                            // Use echo to pipe password to sudo -S, then run the command with yes | for POK-manager prompts
                            // The format: echo 'password' | sudo -S bash -c "yes | ./POK-manager.sh ..."
                            sudoPasswordPart = $"echo '{escapedPassword}' | sudo -S bash -c \"cd {Model.DirectoryPath} && yes | ./POK-manager.sh -{action} {instanceName}\"";
                        }
                    }
                    catch
                    {
                        // If password decryption fails, try without sudo password
                    }
                }
                
                // Use yes | to automatically answer prompts (non-interactive mode)
                // If sudo password is available, use it; otherwise try sudo -n (non-interactive, requires sudoers config)
                if (string.IsNullOrEmpty(sudoPasswordPart))
                {
                    // Try sudo -n first (non-interactive, requires sudoers config)
                    // If that fails, try without sudo (POK-manager.sh might handle sudo internally)
                    command = $"cd {Model.DirectoryPath} && (yes | sudo -n ./POK-manager.sh -{action} {instanceName} 2>&1 || yes | ./POK-manager.sh -{action} {instanceName})";
                }
                else
                {
                    command = sudoPasswordPart;
                }
            }
            else
            {
                command = $"cd {Model.DirectoryPath} && ./POK-manager.sh -{action} {instanceName}";
            }
            
            System.Diagnostics.Debug.WriteLine($"Executing command: {command}");
            string result = await _sshService.ExecuteCommandAsync(command);
            System.Diagnostics.Debug.WriteLine($"{action} command result: {result}");
            
            if (!string.IsNullOrEmpty(result))
            {
                System.Diagnostics.Debug.WriteLine($"Command output: {result}");
            }
            
            if (action == "restart")
            {
                // For restart, we need to wait a bit between stop and start
                // But since we're using a single command, the script handles it
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Parancs végrehajtási hiba ({action}): {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            IsBusy = false;
            // Status will be updated by monitoring service
            OnPropertyChanged(nameof(Status));
        }
    }

    private async Task ExecuteStopAsync()
    {
        if (IsBusy || !_sshService.IsConnected)
            return;

        // Get instance name (without Instance_ prefix) - needed for confirmation dialog
        string instanceName = await GetInstanceNameAsync();

        // Show confirmation dialog for stop action
        string pokManagerPath = $"{Model.DirectoryPath}/POK-manager.sh";
        string fullCommand = $"cd {Model.DirectoryPath} && ./POK-manager.sh -stop {instanceName}";
        string message = $"{LocalizationHelper.GetString("stop_server_confirm")}\n\n" +
                       $"{LocalizationHelper.GetString("pok_manager_path")}: {pokManagerPath}\n\n" +
                       $"Parancs: {fullCommand}";
        
        var result = System.Windows.MessageBox.Show(
            message,
            LocalizationHelper.GetString("stop"),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        // Show status window
        var statusWindow = new Views.StopServerWindow();
        statusWindow.Owner = System.Windows.Application.Current.MainWindow;
        statusWindow.Show();

        IsBusy = true;
        Model.Status = ServerStatus.Busy;
        OnPropertyChanged(nameof(Status));

        try
        {
            // First, save the world
            statusWindow.UpdateStatus(LocalizationHelper.GetString("saving_world"));
            string saveCommand = $"cd {Model.DirectoryPath} && ./POK-manager.sh -saveworld {instanceName}";
            System.Diagnostics.Debug.WriteLine($"Executing saveworld command: {saveCommand}");
            await _sshService.ExecuteCommandAsync(saveCommand);
            
            // Wait 10 seconds
            statusWindow.UpdateStatus($"{LocalizationHelper.GetString("saving_world")} (10 másodperc várakozás...)");
            for (int i = 10; i > 0; i--)
            {
                statusWindow.UpdateStatus($"{LocalizationHelper.GetString("saving_world")} ({i} másodperc...)");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            
            // Then stop the server - use the same sudo password logic as start command
            statusWindow.UpdateStatus(LocalizationHelper.GetString("stopping_server"));
            string stopCommand;
            
            // Get sudo password if available (assume SSH password is same as sudo password)
            string sudoPasswordPart = string.Empty;
            if (_connectionSettings != null && !_connectionSettings.UseSshKey && !string.IsNullOrEmpty(_connectionSettings.EncryptedPassword))
            {
                try
                {
                    string password = EncryptionService.Decrypt(_connectionSettings.EncryptedPassword);
                    if (!string.IsNullOrEmpty(password))
                    {
                        // Escape password for shell (replace single quotes with '\'' and wrap in single quotes)
                        string escapedPassword = password.Replace("'", "'\\''");
                        // Use echo to pipe password to sudo -S, then run the command
                        sudoPasswordPart = $"echo '{escapedPassword}' | sudo -S bash -c \"cd {Model.DirectoryPath} && ./POK-manager.sh -stop {instanceName}\"";
                    }
                }
                catch
                {
                    // If password decryption fails, try without sudo password
                }
            }
            
            // If sudo password is available, use it; otherwise try sudo -n (non-interactive, requires sudoers config)
            if (string.IsNullOrEmpty(sudoPasswordPart))
            {
                // Try sudo -n first (non-interactive, requires sudoers config)
                // If that fails, try without sudo (POK-manager.sh might handle sudo internally)
                stopCommand = $"cd {Model.DirectoryPath} && (sudo -n ./POK-manager.sh -stop {instanceName} 2>&1 || ./POK-manager.sh -stop {instanceName})";
            }
            else
            {
                stopCommand = sudoPasswordPart;
            }
            
            System.Diagnostics.Debug.WriteLine($"Executing stop command: {stopCommand}");
            string commandResult = await _sshService.ExecuteCommandAsync(stopCommand);
            System.Diagnostics.Debug.WriteLine($"Stop command result: {commandResult}");
            
            // Update status window
            statusWindow.UpdateStatus(LocalizationHelper.GetString("server_stopped"));
            statusWindow.SetCompleted();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Leállítási hiba: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            statusWindow.UpdateStatus($"{LocalizationHelper.GetString("error")}: {ex.Message}");
            statusWindow.SetCompleted();
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    private async Task ExecuteShutdownAsync()
    {
        if (IsBusy || !_sshService.IsConnected)
            return;

        // Get instance name (without Instance_ prefix) - needed for confirmation dialog
        string instanceName = await GetInstanceNameAsync();

        // Show input dialog for shutdown time
        var inputDialog = new System.Windows.Window
        {
            Title = LocalizationHelper.GetString("scheduled_shutdown"),
            Width = 400,
            Height = 150,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            ResizeMode = System.Windows.ResizeMode.NoResize
        };

        var stackPanel = new System.Windows.Controls.StackPanel
        {
            Margin = new System.Windows.Thickness(20)
        };

        var label = new System.Windows.Controls.Label
        {
            Content = LocalizationHelper.GetString("shutdown_time_minutes"),
            Margin = new System.Windows.Thickness(0, 0, 0, 10)
        };

        var textBox = new System.Windows.Controls.TextBox
        {
            Height = 30,
            Margin = new System.Windows.Thickness(0, 0, 0, 10)
        };

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var okButton = new System.Windows.Controls.Button
        {
            Content = LocalizationHelper.GetString("confirm"),
            Width = 80,
            Height = 30,
            Margin = new System.Windows.Thickness(0, 0, 10, 0),
            IsDefault = true
        };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = LocalizationHelper.GetString("cancel"),
            Width = 80,
            Height = 30,
            IsCancel = true
        };

        bool? dialogResult = null;
        okButton.Click += (s, e) => { dialogResult = true; inputDialog.Close(); };
        cancelButton.Click += (s, e) => { dialogResult = false; inputDialog.Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        stackPanel.Children.Add(label);
        stackPanel.Children.Add(textBox);
        stackPanel.Children.Add(buttonPanel);

        inputDialog.Content = stackPanel;
        inputDialog.ShowDialog();

        if (dialogResult != true || string.IsNullOrWhiteSpace(textBox.Text))
            return;

        if (!int.TryParse(textBox.Text, out int minutes) || minutes <= 0)
        {
            System.Windows.MessageBox.Show(
                LocalizationHelper.GetString("error") + ": " + LocalizationHelper.GetString("shutdown_time_minutes") + " " + LocalizationHelper.GetString("error"),
                LocalizationHelper.GetString("error"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return;
        }

        // Show confirmation dialog for shutdown action
        string pokManagerPath = $"{Model.DirectoryPath}/POK-manager.sh";
        string fullCommand = $"cd {Model.DirectoryPath} && ./POK-manager.sh -shutdown {minutes} {instanceName}";
        string message = $"{LocalizationHelper.GetString("scheduled_shutdown")}\n\n" +
                       $"{LocalizationHelper.GetString("pok_manager_path")}: {pokManagerPath}\n\n" +
                       $"Parancs: {fullCommand}\n\n" +
                       $"{LocalizationHelper.GetString("shutdown_time_minutes")}: {minutes}";
        
        var confirmResult = System.Windows.MessageBox.Show(
            message,
            LocalizationHelper.GetString("scheduled_shutdown"),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        
        if (confirmResult != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        // Show status window (closable at any time)
        var statusWindow = new Views.StopServerWindow();
        statusWindow.Owner = System.Windows.Application.Current.MainWindow;
        statusWindow.Title = LocalizationHelper.GetString("scheduled_shutdown");
        statusWindow.SetClosable(); // Allow closing immediately
        statusWindow.Show();

        IsBusy = true;
        Model.Status = ServerStatus.Busy;
        OnPropertyChanged(nameof(Status));

        try
        {
            // First, save the world
            statusWindow.UpdateStatus(LocalizationHelper.GetString("saving_world"));
            string saveCommand = $"cd {Model.DirectoryPath} && ./POK-manager.sh -saveworld {instanceName}";
            System.Diagnostics.Debug.WriteLine($"Executing saveworld command: {saveCommand}");
            await _sshService.ExecuteCommandAsync(saveCommand);
            
            // Wait 10 seconds
            statusWindow.UpdateStatus($"{LocalizationHelper.GetString("saving_world")} (10 másodperc várakozás...)");
            for (int i = 10; i > 0; i--)
            {
                statusWindow.UpdateStatus($"{LocalizationHelper.GetString("saving_world")} ({i} másodperc...)");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            
            // Then schedule shutdown
            statusWindow.UpdateStatus($"{LocalizationHelper.GetString("scheduled_shutdown")} ({minutes} perc múlva)...");
            string shutdownCommand = $"cd {Model.DirectoryPath} && ./POK-manager.sh -shutdown {minutes} {instanceName}";
            System.Diagnostics.Debug.WriteLine($"Executing shutdown command: {shutdownCommand}");
            string result = await _sshService.ExecuteCommandAsync(shutdownCommand);
            System.Diagnostics.Debug.WriteLine($"Shutdown command result: {result}");

            // Update status window
            statusWindow.UpdateStatus($"{LocalizationHelper.GetString("shutdown_scheduled")} ({minutes} perc múlva)");
            statusWindow.SetCompleted();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Időzített leállítási hiba: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            statusWindow.UpdateStatus($"{LocalizationHelper.GetString("error")}: {ex.Message}");
            statusWindow.SetCompleted();
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    private async Task ExecuteUpdateAsync()
    {
        if (IsBusy || !_sshService.IsConnected)
            return;

        // Get instance name
        string instanceName = await GetInstanceNameAsync();

        // Show confirmation dialog
        string message = $"Are you sure you want to update the server '{instanceName}'?\n\n" +
                        "This will:\n" +
                        "1. Stop the server\n" +
                        "2. Wait 15 seconds\n" +
                        "3. Run the update command (you can watch the live output)\n" +
                        "4. Wait 10 minutes\n" +
                        "5. Start the server again\n\n" +
                        "Do you want to continue?";

        var result = System.Windows.MessageBox.Show(
            message,
            "Server Update",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        Model.Status = ServerStatus.Busy;
        OnPropertyChanged(nameof(Status));

        try
        {
            // Open update window with live output
            var updateWindow = new Views.UpdateServerWindow(_sshService, Model.DirectoryPath, instanceName);
            updateWindow.Owner = System.Windows.Application.Current.MainWindow;
            updateWindow.Show();

            // Start update process
            await updateWindow.StartUpdateAsync(_connectionSettings);

            // Wait for update to complete (window will show progress)
            // The window can be closed by user, but update will continue in background
        }
        catch (Exception ex)
        {
            LogHelper.WriteToServerCardLog($"Error executing update: {ex.Message}");
            System.Windows.MessageBox.Show(
                $"Error executing update: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(Status));
        }
    }

    private void OnServerStatsUpdated(object? sender, MonitoringService.ServerStatsEventArgs e)
    {
        // Use EXACT case-insensitive matching to avoid mismatches
        string normalizedEventServerName = e.ServerName.ToLowerInvariant().Trim();
        string normalizedModelName = Model.Name.ToLowerInvariant().Trim();
        
        bool isMatch = normalizedEventServerName == normalizedModelName;
        
        System.Diagnostics.Debug.WriteLine($"[ServerCardViewModel] Matching: EventServerName='{e.ServerName}' (normalized: '{normalizedEventServerName}') vs ModelName='{Model.Name}' (normalized: '{normalizedModelName}') -> Match={isMatch}");
        
        if (!isMatch)
        {
            // Only log, don't apply - exact match required to avoid data mixups
            System.Diagnostics.Debug.WriteLine($"[ServerCardViewModel] SKIPPING update for '{Model.Name}' - server name mismatch (event: '{e.ServerName}')");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[ServerCardViewModel] APPLYING update to '{Model.Name}': Status={e.Status}, Players={e.OnlinePlayers}/{e.MaxPlayers}, Day={e.GameDay}");
        
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Model.Status = e.Status;
            Model.CpuUsage = e.CpuUsage;
            Model.MemoryUsage = e.MemoryUsage;
                Model.OnlinePlayers = e.OnlinePlayers;
                Model.MaxPlayers = e.MaxPlayers;
                Model.GameDay = e.GameDay;
                Model.ServerVersion = e.ServerVersion;
                Model.ServerPing = e.ServerPing;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(CpuUsage));
                OnPropertyChanged(nameof(MemoryUsage));
                OnPropertyChanged(nameof(PlayerInfo));
                OnPropertyChanged(nameof(GameDayInfo));
                OnPropertyChanged(nameof(ServerVersionInfo));
                OnPropertyChanged(nameof(ServerPingInfo));
        });
    }

    private void OpenConfigWindow()
    {
        try
        {
            LogHelper.WriteToConfigLog("=== OpenConfigWindow called ===");
            LogHelper.WriteToConfigLog($"SSH Service is null: {_sshService == null}");
            
            if (!_sshService.IsConnected)
            {
                LogHelper.WriteToConfigLog("SSH Service is not connected");
                System.Windows.MessageBox.Show(
                    "Nincs aktív SSH kapcsolat!",
                    "Hiba",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            LogHelper.WriteToConfigLog("SSH Service is connected, creating ConfigService");
            string basePath = _connectionSettings?.ServerBasePath ?? string.Empty;
            if (string.IsNullOrEmpty(basePath) && !string.IsNullOrEmpty(_connectionSettings?.Username))
            {
                basePath = $"/home/{_connectionSettings.Username}/asa_server";
            }
            var configService = new Services.ConfigService(_sshService, basePath);
            LogHelper.WriteToConfigLog($"ConfigService created, ServerName={_model.Name}, BasePath={basePath}");
            
            LogHelper.WriteToConfigLog("Creating ConfigViewModel");
            var configViewModel = new ConfigViewModel(configService, _model, _sshService);
            LogHelper.WriteToConfigLog("ConfigViewModel created successfully");
            
            LogHelper.WriteToConfigLog("Creating ConfigWindow");
            var configWindow = new Views.ConfigWindow(configViewModel);
            LogHelper.WriteToConfigLog("ConfigWindow created successfully, about to show dialog");
            
            configWindow.ShowDialog();
            LogHelper.WriteToConfigLog("ConfigWindow dialog closed");
        }
        catch (Exception ex)
        {
            LogHelper.WriteToConfigLog($"OpenConfigWindow ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}");
            
            System.Windows.MessageBox.Show(
                $"Konfiguráció ablak megnyitási hiba:\n\n{ex.Message}\n\n{ex.StackTrace}\n\nInner: {ex.InnerException?.Message}",
                "Hiba",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OpenLiveLogsWindow()
    {
        try
        {
            if (!_sshService.IsConnected)
            {
                System.Windows.MessageBox.Show(
                    LocalizationHelper.GetString("disconnected"),
                    LocalizationHelper.GetString("error"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Extract instance name from server directory path
            // The directory path is like: /home/user/asa_server/Cluster_Name_servermappaneve
            // We need to extract the servermappaneve part (last segment after underscore)
            string instanceName = _model.Name;
            
            // If the name contains underscores, try to extract the last part
            // For example: "Cluster_Rexodon_aberrationteszt" -> "aberrationteszt"
            if (instanceName.Contains('_'))
            {
                string[] parts = instanceName.Split('_');
                if (parts.Length > 1)
                {
                    // Take the last part as instance name
                    instanceName = parts[parts.Length - 1];
                }
            }

            var liveLogsWindow = new Views.LiveLogsWindow(_sshService, _model.DirectoryPath, instanceName);
            liveLogsWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Hiba az élő logok megnyitásakor:\n\n{ex.Message}",
                LocalizationHelper.GetString("error"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task OpenDockerSetupWindowAsync()
    {
        try
        {
            if (!_sshService.IsConnected)
            {
                System.Windows.MessageBox.Show(
                    LocalizationHelper.GetString("disconnected"),
                    LocalizationHelper.GetString("error"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Get instance name
            string instanceName = await GetInstanceNameAsync();
            
            if (string.IsNullOrEmpty(instanceName))
            {
                System.Windows.MessageBox.Show(
                    "Could not determine instance name for Docker setup.",
                    LocalizationHelper.GetString("error"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var dockerSetupWindow = new Views.DockerSetupWindow(_sshService, _model.DirectoryPath, instanceName);
                dockerSetupWindow.Owner = System.Windows.Application.Current.MainWindow;
                dockerSetupWindow.ShowDialog();
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error opening Docker setup window:\n\n{ex.Message}",
                LocalizationHelper.GetString("error"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}

public class RelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public async void Execute(object? parameter)
    {
        await _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public class RelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<T?, Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public async void Execute(object? parameter)
    {
        T? typedParameter = parameter is T t ? t : default;
        await _execute(typedParameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
