using System.Windows;
using System.Windows.Threading;
using ZedASAManager.ViewModels;
using ZedASAManager.Utilities;

namespace ZedASAManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        try
        {
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", $"MainWindow constructor started\n");
            }
            catch { }
            
            InitializeComponent();
            
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", $"MainWindow InitializeComponent() completed\n");
            }
            catch { }
            
            // Initialize ViewModel after the window is loaded
            this.Loaded += MainWindow_Loaded;
            
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", $"MainWindow constructor completed, Loaded event handler registered\n");
            }
            catch { }
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", $"MAIN WINDOW INIT ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}\n");
            }
            catch { }
            
            MessageBox.Show(
                $"Ablak inicializálási hiba:\n\n{ex.Message}\n\n{ex.StackTrace}\n\nInner: {ex.InnerException?.Message}",
                "Hiba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            // Don't throw, let the app handle it
            Application.Current?.Shutdown();
        }
    }

    public string? LoggedInUsername { get; set; }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", $"MainWindow_Loaded called, LoggedInUsername={LoggedInUsername}\n");
            }
            catch { }
            
            if (string.IsNullOrEmpty(LoggedInUsername))
            {
                // If no username provided, show error and close window
                try
                {
                    System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", "MainWindow_Loaded: No username, closing window\n");
                }
                catch { }
                
                MessageBox.Show(
                    "Nincs bejelentkezett felhasználó!",
                    "Hiba",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Close();
                return;
            }

            // Initialize ViewModel
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", "MainWindow_Loaded: Creating MainViewModel...\n");
            }
            catch { }
            
            var viewModel = new MainViewModel(LoggedInUsername);
            DataContext = viewModel;
            
            // Load localized strings for buttons
            LoadLocalizedStrings();
            
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", "MainWindow_Loaded: MainViewModel created successfully\n");
            }
            catch { }
            
            // Automatikusan kapcsolódunk az első szerverhez, ha van
            if (viewModel.SavedServers.Count > 0)
            {
                try
                {
                    System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", $"MainWindow_Loaded: Auto-connecting to first server: {viewModel.SavedServers[0].Name}\n");
                }
                catch { }
                
                // Kis késleltetés után automatikusan kapcsolódunk
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await viewModel.ConnectAsync();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", $"Auto-connect error: {ex.Message}\n");
                        }
                        catch { }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            
            // Ensure window is visible
            this.Activate();
            this.Focus();
            
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", $"MainWindow_Loaded: Window activated, Visibility={this.Visibility}, IsVisible={this.IsVisible}\n");
            }
            catch { }
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", $"MAIN VIEWMODEL ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}\n");
            }
            catch { }
            
            MessageBox.Show(
                $"ViewModel inicializálási hiba:\n\n{ex.Message}\n\n{ex.StackTrace}\n\nInner: {ex.InnerException?.Message}",
                "Hiba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            // Don't close window on error, let user see the error
            // But ensure window is still visible
            this.Visibility = Visibility.Visible;
            this.Show();
            this.Activate();
        }
    }

    private void ViewTermsButton_Click(object sender, RoutedEventArgs e)
    {
        var termsWindow = new Views.TermsWindow();
        termsWindow.Owner = this;
        termsWindow.ShowDialog();
    }

    private void LoadLocalizedStrings()
    {
        if (ClusterManagementButton != null)
        {
            ClusterManagementButton.Content = LocalizationHelper.GetString("cluster_management");
        }
    }
}
