using System.Windows;
using ZedASAManager.ViewModels;
using System.IO;

namespace ZedASAManager.Views;

public partial class ConfigWindow : Window
{
    public ConfigWindow(ConfigViewModel viewModel)
    {
        try
        {
            File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigWindow constructor started\n");
            File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigWindow: ViewModel is null: {viewModel == null}\n");
            
            if (viewModel != null)
            {
                File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigWindow: ServerName={viewModel.ServerName}\n");
            }
            
            File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigWindow: About to call InitializeComponent()\n");
            InitializeComponent();
            File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigWindow: InitializeComponent() completed\n");
            
            File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigWindow: About to set DataContext\n");
            DataContext = viewModel;
            File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigWindow: DataContext set successfully\n");
            
            File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigWindow constructor completed successfully\n");
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigWindow constructor ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}\n");
            }
            catch { }
            
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
        try
        {
            File.AppendAllText("C:\\temp\\zedasa_config_debug.log", $"ConfigWindow: CloseButton_Click called\n");
        }
        catch { }
        Close();
    }
}
