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
            LogHelper.WriteToConfigLog("ConfigViewModel constructor started");
            LogHelper.WriteToConfigLog($"ConfigService is null: {configService == null}");
            LogHelper.WriteToConfigLog($"ServerInstance is null: {serverInstance == null}");
            
            if (serverInstance != null)
            {
                LogHelper.WriteToConfigLog($"ServerInstance.Name={serverInstance.Name}");
            }
            
            _configService = configService;
            _serverInstance = serverInstance;
            _sshService = sshService;
            
            LogHelper.WriteToConfigLog("Creating commands");
            SaveCommand = new RelayCommandSyncTask(async () => await SaveConfigAsync(), () => !IsLoading);
            DownloadCommand = new RelayCommandSyncTask(async () => await DownloadConfigAsync(), () => !IsLoading);
            UploadCommand = new RelayCommandSyncTask(async () => await UploadConfigAsync(), () => !IsLoading);
            LoadCommand = new RelayCommandSyncTask(async () => await LoadConfigAsync(), () => !IsLoading);
            OpenTextEditorCommand = new RelayCommandSync(() => OpenTextEditorWindow());
            
            LogHelper.WriteToConfigLog("ConfigViewModel constructor completed successfully");
        }
        catch (Exception ex)
        {
            LogHelper.WriteToConfigLog($"ConfigViewModel constructor ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}");
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
    public ICommand OpenTextEditorCommand { get; }
    public ICommand OpenLiveLogsCommand { get; }

    public async Task LoadConfigAsync()
    {
        LogHelper.WriteToConfigLog("=== LoadConfigAsync called ===");
        
        if (IsLoading)
        {
            LogHelper.WriteToConfigLog("LoadConfigAsync: Already loading, skipping");
            return;
        }
        
        IsLoading = true;
        LogHelper.WriteToConfigLog($"LoadConfigAsync: IsLoading set to true");
        
        try
        {
            LogHelper.WriteToConfigLog($"Loading config files for server: {_serverInstance?.Name ?? "null"}");
            
            if (_serverInstance == null)
            {
                throw new Exception("ServerInstance is null");
            }
            
            if (_configService == null)
            {
                throw new Exception("ConfigService is null");
            }
            
            // Load both config files
            LogHelper.WriteToConfigLog($"LoadConfigAsync: Server DirectoryPath={_serverInstance.DirectoryPath}");
            LogHelper.WriteToConfigLog("LoadConfigAsync: Reading Game.ini...");
            string gameIniContent = await _configService.ReadConfigFileAsync(_serverInstance.DirectoryPath, "Game.ini");
            LogHelper.WriteToConfigLog($"Game.ini loaded, length: {gameIniContent?.Length ?? 0}");
            
            LogHelper.WriteToConfigLog("LoadConfigAsync: Reading GameUserSettings.ini...");
            string gameUserSettingsIniContent = await _configService.ReadConfigFileAsync(_serverInstance.DirectoryPath, "GameUserSettings.ini");
            LogHelper.WriteToConfigLog($"GameUserSettings.ini loaded, length: {gameUserSettingsIniContent?.Length ?? 0}");

            LogHelper.WriteToConfigLog("LoadConfigAsync: Parsing Game.ini...");
            GameIni = _configService.ParseIniFile(gameIniContent ?? string.Empty);
            LogHelper.WriteToConfigLog($"GameIni parsed, sections: {GameIni?.Sections?.Count ?? 0}");
            
            LogHelper.WriteToConfigLog("LoadConfigAsync: Parsing GameUserSettings.ini...");
            GameUserSettingsIni = _configService.ParseIniFile(gameUserSettingsIniContent ?? string.Empty);
            LogHelper.WriteToConfigLog($"GameUserSettingsIni parsed, sections: {GameUserSettingsIni?.Sections?.Count ?? 0}");
            
            LogHelper.WriteToConfigLog("LoadConfigAsync: Raising property changed events...");
            
            // Frissítjük az összes property-t a UI-ban
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(CurrentIniFile));
                OnPropertyChanged(nameof(GameIni));
                OnPropertyChanged(nameof(GameUserSettingsIni));
            });
            
            LogHelper.WriteToConfigLog("LoadConfigAsync completed successfully");
        }
        catch (Exception ex)
        {
            LogHelper.WriteToConfigLog($"LoadConfigAsync error: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}");
            
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show(
                    $"Konfiguráció betöltési hiba:\n\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace}",
                    "Hiba",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            });
        }
        finally
        {
            IsLoading = false;
            LogHelper.WriteToConfigLog($"LoadConfigAsync finished, IsLoading={IsLoading}");
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
            await _configService.SaveConfigFileAsync(_serverInstance.DirectoryPath, SelectedConfigFile, content);

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

    private void OpenTextEditorWindow()
    {
        try
        {
            if (!_sshService.IsConnected)
            {
                System.Windows.MessageBox.Show(
                    "Nincs aktív SSH kapcsolat!",
                    "Hiba",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var textEditorWindow = new Views.ConfigTextEditorWindow(_configService, _serverInstance.DirectoryPath, _serverInstance.Name);
                textEditorWindow.Owner = System.Windows.Application.Current.MainWindow;
                textEditorWindow.ShowDialog();
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Hiba a szöveges szerkesztő megnyitásakor:\n\n{ex.Message}",
                "Hiba",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
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
        try
        {
            await _execute();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RelayCommandSyncTask Execute error: {ex.Message}\n{ex.StackTrace}");
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show(
                    $"Command execution error: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            });
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

