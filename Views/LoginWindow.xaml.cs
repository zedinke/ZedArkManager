using System.Windows;
using System.Windows.Controls;
using ZedASAManager.ViewModels;
using ZedASAManager.Services;

namespace ZedASAManager.Views;

public partial class LoginWindow : Window
{
    private LoginViewModel _viewModel;

    public string? LoggedInUsername { get; private set; }

    public LoginWindow()
    {
        try
        {
            // Check if application is shutting down before initializing
            if (Application.Current == null || Application.Current.ShutdownMode == ShutdownMode.OnExplicitShutdown)
            {
                System.Diagnostics.Debug.WriteLine("LoginWindow: Application is shutting down, cannot initialize");
                return;
            }
            
            InitializeComponent();
            _viewModel = new LoginViewModel();
            DataContext = _viewModel;

            _viewModel.LoginSuccessful += OnLoginSuccessful;
            _viewModel.RegisterSuccessful += OnRegisterSuccessful;
            
            // Set version number
            LoadVersion();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("shut down") || ex.Message.Contains("Shutdown") || ex.Message.Contains("being shut"))
        {
            // Application is shutting down, don't show error
            System.Diagnostics.Debug.WriteLine($"LoginWindow: Application shutting down: {ex.Message}");
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", $"LoginWindow: Application shutting down exception caught: {ex.Message}\n");
            }
            catch { }
            return;
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText("C:\\temp\\zedasa_startup.log", $"LoginWindow: Exception in constructor: {ex.Message}\n{ex.StackTrace}\n");
            }
            catch { }
            
            MessageBox.Show(
                $"LoginWindow inicializálási hiba:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Hiba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            throw;
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.Password = passwordBox.Password;
        }
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.Username) && !string.IsNullOrWhiteSpace(_viewModel.Password))
        {
            _viewModel.Login();
        }
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        var registerWindow = new RegisterWindow();
        registerWindow.Owner = this;
        bool? result = registerWindow.ShowDialog();

        if (result == true && !string.IsNullOrEmpty(registerWindow.RegisteredUsername))
        {
            // Automatikusan bejelentkeztetjük az új felhasználót
            _viewModel.Username = registerWindow.RegisteredUsername;
            UsernameTextBox.Text = registerWindow.RegisteredUsername;
            PasswordBox.Focus();
        }
    }

    private void ViewTermsButton_Click(object sender, RoutedEventArgs e)
    {
        var termsWindow = new TermsWindow();
        termsWindow.Owner = this;
        termsWindow.ShowDialog();
    }

    private void OnLoginSuccessful(object? sender, string username)
    {
        LoggedInUsername = username;
        
        // Use BeginInvoke to ensure window is fully loaded before setting DialogResult
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (IsLoaded)
                {
                    DialogResult = true;
                }
                else
                {
                    // If window is not loaded yet, wait for Loaded event
                    Loaded += (s, e) =>
                    {
                        try
                        {
                            DialogResult = true;
                        }
                        catch
                        {
                            Close();
                        }
                    };
                }
            }
            catch
            {
                // If DialogResult can't be set, just close the window
                Close();
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnRegisterSuccessful(object? sender, string username)
    {
        MessageBox.Show(
            "Regisztráció sikeres! Most bejelentkezhetsz.",
            "Sikeres regisztráció",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    
    private void LoadVersion()
    {
        try
        {
            var updateService = new UpdateService();
            string version = updateService.GetCurrentVersion();
            VersionTextBlock.Text = $"Verzió {version}";
        }
        catch
        {
            VersionTextBlock.Text = "Verzió ismeretlen";
        }
    }
}
