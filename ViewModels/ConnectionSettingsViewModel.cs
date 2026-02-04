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
    private ConnectionSettings _settings;
    private string _password = string.Empty;
    private bool _useSshKey;

    public ConnectionSettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settings = _settingsService.LoadConnectionSettings() ?? new ConnectionSettings();

        SaveCommand = new RelayCommandSync(() => SaveSettings(), () => !string.IsNullOrEmpty(Host) && !string.IsNullOrEmpty(Username));
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

    public ICommand SaveCommand { get; }

    private void SaveSettings()
    {
        _settingsService.SaveConnectionSettings(_settings);
    }
}
