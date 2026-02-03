using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using ZedASAManager.Models;
using ZedASAManager.Services;
using ZedASAManager.Utilities;
using Microsoft.Win32;
using System.IO;

namespace ZedASAManager.ViewModels;

public class ConfigViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly ServerInstance _serverInstance;
    private readonly SshService _sshService;
    private IniFile? _gameIni;
    private IniFile? _gameUserSettingsIni;
    private bool _isLoading;
    private string _selectedConfigFile = "Game.ini";

    public ConfigViewModel(ConfigService configService, ServerInstance serverInstance, SshService sshService)
    {
        try
        {
            System.IO.File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigViewModel constructor started\n");
            System.IO.File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigService is null: {configService == null}\n");
            System.IO.File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ServerInstance is null: {serverInstance == null}\n");
            
            if (serverInstance != null)
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ServerInstance.Name={serverInstance.Name}\n");
            }
            
            _configService = configService;
            _serverInstance = serverInstance;
            _sshService = sshService;
            
            System.IO.File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"Creating commands\n");
            SaveCommand = new RelayCommandSyncTask(async () => await SaveConfigAsync(), () => !IsLoading);
            DownloadCommand = new RelayCommandSyncTask(async () => await DownloadConfigAsync(), () => !IsLoading);
            UploadCommand = new RelayCommandSyncTask(async () => await UploadConfigAsync(), () => !IsLoading);
            LoadCommand = new RelayCommandSyncTask(async () => await LoadConfigAsync(), () => !IsLoading);
            
            System.IO.File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigViewModel constructor completed successfully\n");
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigViewModel constructor ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}\n");
            }
            catch { }
            throw;
        }
    }

    public string ServerName => _serverInstance.Name;
    public string SelectedConfigFile
    {
        get => _selectedConfigFile;
        set
        {
            if (SetProperty(ref _selectedConfigFile, value))
            {
                OnPropertyChanged(nameof(CurrentIniFile));
            }
        }
    }

    public ObservableCollection<string> ConfigFiles { get; } = new() { "Game.ini", "GameUserSettings.ini" };

    public IniFile? GameIni
    {
        get => _gameIni;
        set => SetProperty(ref _gameIni, value);
    }

    public IniFile? GameUserSettingsIni
    {
        get => _gameUserSettingsIni;
        set => SetProperty(ref _gameUserSettingsIni, value);
    }

    public IniFile? CurrentIniFile => SelectedConfigFile == "Game.ini" ? GameIni : GameUserSettingsIni;

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                ((RelayCommandSyncTask)SaveCommand).RaiseCanExecuteChanged();
                ((RelayCommandSyncTask)DownloadCommand).RaiseCanExecuteChanged();
                ((RelayCommandSyncTask)UploadCommand).RaiseCanExecuteChanged();
                ((RelayCommandSyncTask)LoadCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand DownloadCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand OpenLiveLogsCommand { get; }

    public async Task LoadConfigAsync()
    {
        IsLoading = true;
        try
        {
            // Load both config files
            string gameIniContent = await _configService.ReadConfigFileAsync(_serverInstance.Name, "Game.ini");
            string gameUserSettingsIniContent = await _configService.ReadConfigFileAsync(_serverInstance.Name, "GameUserSettings.ini");

            GameIni = _configService.ParseIniFile(gameIniContent);
            GameUserSettingsIni = _configService.ParseIniFile(gameUserSettingsIniContent);

            OnPropertyChanged(nameof(CurrentIniFile));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Konfiguráció betöltési hiba:\n\n{ex.Message}",
                "Hiba",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SaveConfigAsync()
    {
        IsLoading = true;
        try
        {
            if (CurrentIniFile == null)
            {
                System.Windows.MessageBox.Show(
                    "Nincs betöltött konfiguráció!",
                    "Hiba",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            string content = _configService.SerializeIniFile(CurrentIniFile);
            await _configService.SaveConfigFileAsync(_serverInstance.Name, SelectedConfigFile, content);

            System.Windows.MessageBox.Show(
                "Konfiguráció sikeresen mentve!",
                "Siker",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Konfiguráció mentési hiba:\n\n{ex.Message}",
                "Hiba",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task DownloadConfigAsync()
    {
        IsLoading = true;
        try
        {
            if (CurrentIniFile == null)
            {
                System.Windows.MessageBox.Show(
                    "Nincs betöltött konfiguráció!",
                    "Hiba",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                FileName = SelectedConfigFile,
                Filter = "INI fájlok (*.ini)|*.ini|Minden fájl (*.*)|*.*",
                DefaultExt = "ini"
            };

            if (saveDialog.ShowDialog() == true)
            {
                string content = _configService.SerializeIniFile(CurrentIniFile);
                await File.WriteAllTextAsync(saveDialog.FileName, content, Encoding.UTF8);

                System.Windows.MessageBox.Show(
                    "Konfiguráció sikeresen letöltve!",
                    "Siker",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Konfiguráció letöltési hiba:\n\n{ex.Message}",
                "Hiba",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task UploadConfigAsync()
    {
        IsLoading = true;
        try
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "INI fájlok (*.ini)|*.ini|Minden fájl (*.*)|*.*",
                DefaultExt = "ini"
            };

            if (openDialog.ShowDialog() == true)
            {
                string content = await File.ReadAllTextAsync(openDialog.FileName, Encoding.UTF8);
                IniFile iniFile = _configService.ParseIniFile(content);

                if (SelectedConfigFile == "Game.ini")
                {
                    GameIni = iniFile;
                }
                else
                {
                    GameUserSettingsIni = iniFile;
                }

                OnPropertyChanged(nameof(CurrentIniFile));

                System.Windows.MessageBox.Show(
                    "Konfiguráció sikeresen feltöltve! Kattintson a Mentés gombra a szerverre való mentéshez.",
                    "Siker",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Konfiguráció feltöltési hiba:\n\n{ex.Message}",
                "Hiba",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

}

public class RelayCommandSyncTask : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommandSyncTask(Func<Task> execute, Func<bool>? canExecute = null)
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

