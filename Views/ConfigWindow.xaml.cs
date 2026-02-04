using System.Windows;
using ZedASAManager.ViewModels;
using System.IO;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public partial class ConfigWindow : Window
{
    public ConfigWindow(ConfigViewModel viewModel)
    {
        try
        {
            LogHelper.WriteToConfigLog("ConfigWindow constructor started");
            LogHelper.WriteToConfigLog($"ConfigWindow: ViewModel is null: {viewModel == null}");
            
            if (viewModel != null)
            {
                LogHelper.WriteToConfigLog($"ConfigWindow: ServerName={viewModel.ServerName}");
            }
            
            LogHelper.WriteToConfigLog("ConfigWindow: About to call InitializeComponent()");
            InitializeComponent();
            LogHelper.WriteToConfigLog("ConfigWindow: InitializeComponent() completed");
            
            LogHelper.WriteToConfigLog("ConfigWindow: About to set DataContext");
            DataContext = viewModel;
            LogHelper.WriteToConfigLog("ConfigWindow: DataContext set successfully");
            
            // Automatikusan betöltjük a konfigurációt amikor az ablak megnyílik
            // De csak akkor, ha még nincs betöltve
            this.Loaded += async (s, e) =>
            {
                try
                {
                    LogHelper.WriteToConfigLog("ConfigWindow: Loaded event fired");
                    // Csak akkor töltjük be automatikusan, ha még nincs betöltve
                    if (viewModel.GameIni == null && viewModel.GameUserSettingsIni == null)
                    {
                        LogHelper.WriteToConfigLog("ConfigWindow: No config loaded yet, loading automatically");
                        await viewModel.LoadConfigAsync();
                        LogHelper.WriteToConfigLog("ConfigWindow: Config files loaded successfully");
                    }
                    else
                    {
                        LogHelper.WriteToConfigLog("ConfigWindow: Config already loaded, skipping auto-load");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteToConfigLog($"ConfigWindow: Error loading config files: {ex.Message}\n{ex.StackTrace}");
                    // Ne tiltsuk le a gombot, ha az automatikus betöltés sikertelen
                    viewModel.IsLoading = false;
                }
            };
            
            LogHelper.WriteToConfigLog("ConfigWindow constructor completed successfully");
        }
        catch (Exception ex)
        {
            LogHelper.WriteToConfigLog($"ConfigWindow constructor ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}");
            
            MessageBox.Show(
                $"ConfigWindow inicializálási hiba:\n\n{ex.Message}\n\n{ex.StackTrace}\n\nInner: {ex.InnerException?.Message}",
                "Hiba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            throw;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        LogHelper.WriteToConfigLog("ConfigWindow: CloseButton_Click called");
        Close();
    }
}
