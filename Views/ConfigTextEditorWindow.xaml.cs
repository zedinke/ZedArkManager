using System.Windows;
using System.Windows.Controls;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public partial class ConfigTextEditorWindow : Window
{
    private readonly ConfigService _configService;
    private readonly string _serverDirectoryPath;
    private string _currentConfigFile = "Game.ini";

    public ConfigTextEditorWindow(ConfigService configService, string serverDirectoryPath, string serverName)
    {
        InitializeComponent();
        _configService = configService;
        _serverDirectoryPath = serverDirectoryPath;
        
        TitleTextBlock.Text = $"{LocalizationHelper.GetString("text_editor")}: {serverName}";
        LoadButton.Content = LocalizationHelper.GetString("load");
        SaveButton.Content = LocalizationHelper.GetString("save");
        
        // Initialize ComboBox
        ConfigFileComboBox.Items.Add("Game.ini");
        ConfigFileComboBox.Items.Add("GameUserSettings.ini");
        ConfigFileComboBox.SelectedItem = "Game.ini";
        
        // Load initial file
        _ = LoadConfigFileAsync();
    }

    private async void ConfigFileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConfigFileComboBox.SelectedItem != null)
        {
            _currentConfigFile = ConfigFileComboBox.SelectedItem.ToString() ?? "Game.ini";
            await LoadConfigFileAsync();
        }
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadConfigFileAsync();
    }

    private async Task LoadConfigFileAsync()
    {
        try
        {
            LoadButton.IsEnabled = false;
            LoadButton.Content = LocalizationHelper.GetString("loading") + "...";
            ConfigTextBox.IsEnabled = false;
            
            string content = await _configService.ReadConfigFileAsync(_serverDirectoryPath, _currentConfigFile);
            ConfigTextBox.Text = content ?? string.Empty;
            
            ConfigTextBox.IsEnabled = true;
            LoadButton.Content = LocalizationHelper.GetString("load");
            LoadButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Hiba a konfiguráció betöltésekor:\n\n{ex.Message}",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            LoadButton.Content = LocalizationHelper.GetString("load");
            LoadButton.IsEnabled = true;
            ConfigTextBox.IsEnabled = true;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveButton.IsEnabled = false;
            SaveButton.Content = LocalizationHelper.GetString("saving") + "...";
            
            string content = ConfigTextBox.Text;
            await _configService.SaveConfigFileAsync(_serverDirectoryPath, _currentConfigFile, content);
            
            MessageBox.Show(
                LocalizationHelper.GetString("config_saved_successfully"),
                LocalizationHelper.GetString("success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            
            SaveButton.Content = LocalizationHelper.GetString("save");
            SaveButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Hiba a konfiguráció mentésekor:\n\n{ex.Message}",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            SaveButton.Content = LocalizationHelper.GetString("save");
            SaveButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
