using System.Collections.ObjectModel;
using System.Windows.Input;
using ZedASAManager.Models;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.ViewModels;

public class RelayCommandSync : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommandSync(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public class ConnectionSettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly ServerDataService _serverDataService;
    private readonly string _username;
    private ConnectionSettings _settings;
    private string _password = string.Empty;
    private bool _useSshKey;
    private ObservableCollection<SavedServer> _savedServers = new();
    private SavedServer? _selectedSavedServer;
    private bool _isNewServer;

    public ConnectionSettingsViewModel(SettingsService settingsService, ServerDataService serverDataService, string username, ObservableCollection<SavedServer> savedServers, SavedServer? selectedSavedServer = null, bool isNewServer = false)
    {
        _settingsService = settingsService;
        _serverDataService = serverDataService;
        _username = username;
        _savedServers = savedServers;
        _selectedSavedServer = selectedSavedServer;
        _isNewServer = isNewServer;

        // Ha van kiválasztott szerver, annak adatait használjuk
        if (_selectedSavedServer != null)
        {
                _settings = _serverDataService.ConvertToConnectionSettings(_selectedSavedServer);
                if (!string.IsNullOrEmpty(_settings.EncryptedPassword))
                {
                    try
                    {
                        _password = EncryptionService.Decrypt(_settings.EncryptedPassword);
                    }
                    catch
                    {
                        _password = string.Empty;
                    }
                }
                _useSshKey = _settings.UseSshKey;
                
                // Ha nincs ServerBasePath, generáljuk
                if (string.IsNullOrEmpty(_settings.ServerBasePath) && !string.IsNullOrEmpty(_settings.Username))
                {
                    _settings.ServerBasePath = $"/home/{_settings.Username}/asa_server";
                }
        }
        else
        {
            _settings = _settingsService.LoadConnectionSettings() ?? new ConnectionSettings();
        }

        SaveCommand = new RelayCommandSync(() => SaveSettings(), () => !string.IsNullOrEmpty(Host) && !string.IsNullOrEmpty(Username) && SelectedSavedServer != null);
    }

    public string SaveButtonText
    {
        get => _isNewServer ? LocalizationHelper.GetString("add_host") : LocalizationHelper.GetString("save");
        set { } // Setter needed for binding
    }

    public ConnectionSettings Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }

    public string Host
    {
        get => _settings.Host;
        set
        {
            _settings.Host = value;
            OnPropertyChanged();
        }
    }

    public int Port
    {
        get => _settings.Port;
        set
        {
            _settings.Port = value;
            OnPropertyChanged();
        }
    }

    public string Username
    {
        get => _settings.Username;
        set
        {
            _settings.Username = value;
            OnPropertyChanged();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            SetProperty(ref _password, value);
            if (!string.IsNullOrEmpty(value))
            {
                _settings.EncryptedPassword = EncryptionService.Encrypt(value);
            }
        }
    }

    public string? SshKeyPath
    {
        get => _settings.SshKeyPath;
        set
        {
            _settings.SshKeyPath = value;
            OnPropertyChanged();
        }
    }

    public bool UseSshKey
    {
        get => _useSshKey;
        set
        {
            SetProperty(ref _useSshKey, value);
            _settings.UseSshKey = value;
        }
    }

    public string ServerBasePath
    {
        get => _settings.ServerBasePath;
        set
        {
            _settings.ServerBasePath = value;
            OnPropertyChanged();
        }
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
            if (SetProperty(ref _selectedSavedServer, value) && value != null)
            {
                // Betöltjük a kiválasztott szerver adatait
                _settings = _serverDataService.ConvertToConnectionSettings(value);
                if (!string.IsNullOrEmpty(_settings.EncryptedPassword))
                {
                    try
                    {
                        _password = EncryptionService.Decrypt(_settings.EncryptedPassword);
                    }
                    catch
                    {
                        _password = string.Empty;
                    }
                }
                else
                {
                    _password = string.Empty;
                }
                _useSshKey = _settings.UseSshKey;
                
                // Ha nincs ServerBasePath, generáljuk
                if (string.IsNullOrEmpty(_settings.ServerBasePath) && !string.IsNullOrEmpty(_settings.Username))
                {
                    _settings.ServerBasePath = $"/home/{_settings.Username}/asa_server";
                }
                
                // Frissítjük az összes property-t
                OnPropertyChanged(nameof(Host));
                OnPropertyChanged(nameof(Port));
                OnPropertyChanged(nameof(Username));
                OnPropertyChanged(nameof(Password));
                OnPropertyChanged(nameof(UseSshKey));
                OnPropertyChanged(nameof(SshKeyPath));
                OnPropertyChanged(nameof(ServerBasePath));
            }
        }
    }

    public ICommand SaveCommand { get; }

    private void SaveSettings()
    {
        if (SelectedSavedServer == null)
            return;

        // Frissítjük a kiválasztott szerver adatait
        SelectedSavedServer.Host = Host;
        SelectedSavedServer.Port = Port;
        SelectedSavedServer.Username = Username;
        SelectedSavedServer.EncryptedPassword = _settings.EncryptedPassword;
        SelectedSavedServer.SshKeyPath = SshKeyPath;
        SelectedSavedServer.UseSshKey = UseSshKey;
        SelectedSavedServer.ServerBasePath = ServerBasePath;

        // Mentjük a szervereket
        var servers = _serverDataService.LoadServers(_username);
        var existingServer = servers.FirstOrDefault(s => s.Name == SelectedSavedServer.Name);
        if (existingServer != null)
        {
            existingServer.Host = SelectedSavedServer.Host;
            existingServer.Port = SelectedSavedServer.Port;
            existingServer.Username = SelectedSavedServer.Username;
            existingServer.EncryptedPassword = SelectedSavedServer.EncryptedPassword;
            existingServer.SshKeyPath = SelectedSavedServer.SshKeyPath;
            existingServer.UseSshKey = SelectedSavedServer.UseSshKey;
            existingServer.ServerBasePath = SelectedSavedServer.ServerBasePath;
        }
        _serverDataService.SaveServers(_username, servers);
    }
}
