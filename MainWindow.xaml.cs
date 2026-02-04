using System.Windows;
using System.Windows.Threading;
using ZedASAManager.ViewModels;
using ZedASAManager.Utilities;
using ZedASAManager.Services;

namespace ZedASAManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        try
        {
            LogHelper.WriteToStartupLog("MainWindow constructor started");
            
            InitializeComponent();
            
            LogHelper.WriteToStartupLog("MainWindow InitializeComponent() completed");
            
            // Initialize ViewModel after the window is loaded
            this.Loaded += MainWindow_Loaded;
            
            LogHelper.WriteToStartupLog("MainWindow constructor completed, Loaded event handler registered");
        }
        catch (Exception ex)
        {
            LogHelper.WriteToStartupLog($"MAIN WINDOW INIT ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}");
            
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
            LogHelper.WriteToStartupLog($"MainWindow_Loaded called, LoggedInUsername={LoggedInUsername}");
            
            if (string.IsNullOrEmpty(LoggedInUsername))
            {
                // If no username provided, show error and close window
                LogHelper.WriteToStartupLog("MainWindow_Loaded: No username, closing window");
                
                MessageBox.Show(
                    "Nincs bejelentkezett felhasználó!",
                    "Hiba",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Close();
                return;
            }

            // Initialize ViewModel
            LogHelper.WriteToStartupLog("MainWindow_Loaded: Creating MainViewModel...");
            
            var viewModel = new MainViewModel(LoggedInUsername);
            DataContext = viewModel;
            
            // Load localized strings for buttons
            LoadLocalizedStrings();
            
            // Load version number
            LoadVersion();
            
            LogHelper.WriteToStartupLog("MainWindow_Loaded: MainViewModel created successfully");
            
            // Automatikusan kapcsolódunk az első szerverhez, ha van
            if (viewModel.SavedServers.Count > 0)
            {
                LogHelper.WriteToStartupLog($"MainWindow_Loaded: Auto-connecting to first server: {viewModel.SavedServers[0].Name}");
                
                // Kis késleltetés után automatikusan kapcsolódunk
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await viewModel.ConnectAsync();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteToStartupLog($"Auto-connect error: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            
            // Ensure window is visible
            this.Activate();
            this.Focus();
            
            LogHelper.WriteToStartupLog($"MainWindow_Loaded: Window activated, Visibility={this.Visibility}, IsVisible={this.IsVisible}");
        }
        catch (Exception ex)
        {
            LogHelper.WriteToStartupLog($"MAIN VIEWMODEL ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}");
            
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
        if (ChangelogButton != null)
        {
            ChangelogButton.Content = LocalizationHelper.GetString("changelog");
        }
        if (AddServerButton != null)
        {
            AddServerButton.Content = LocalizationHelper.GetString("add_server");
        }
    }

    private void AddServerButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button)
        {
            button.Content = LocalizationHelper.GetString("add_server");
        }
    }

    private void SettingsButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button)
        {
            button.Content = LocalizationHelper.GetString("manager_settings");
        }
    }

    private void ChangelogButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button)
        {
            button.Content = LocalizationHelper.GetString("changelog");
        }
    }

    private void ConnectButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button)
        {
            button.Content = LocalizationHelper.GetString("connect");
        }
    }

    private void DisconnectButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button)
        {
            button.Content = LocalizationHelper.GetString("disconnect");
        }
    }

    private void AddSavedServerButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button)
        {
            button.Content = LocalizationHelper.GetString("add_ssh_connection");
        }
    }

    private void RemoveSavedServerButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button)
        {
            button.Content = LocalizationHelper.GetString("delete_ssh_connection");
        }
    }
    
    private void LoadVersion()
    {
        try
        {
            var updateService = new UpdateService();
            string version = updateService.GetCurrentVersion();
            VersionTextBlock.Text = $"v{version}";
        }
        catch
        {
            VersionTextBlock.Text = "v?";
        }
    }
}
