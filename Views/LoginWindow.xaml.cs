using System.Windows;
using System.Windows.Controls;
using ZedASAManager.ViewModels;

namespace ZedASAManager.Views;

public partial class LoginWindow : Window
{
    private LoginViewModel _viewModel;

    public string? LoggedInUsername { get; private set; }

    public LoginWindow()
    {
        try
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            DataContext = _viewModel;

            _viewModel.LoginSuccessful += OnLoginSuccessful;
            _viewModel.RegisterSuccessful += OnRegisterSuccessful;
        }
        catch (Exception ex)
        {
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
}
