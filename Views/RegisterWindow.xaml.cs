using System.Windows;
using System.Windows.Controls;
using ZedASAManager.ViewModels;

namespace ZedASAManager.Views;

public partial class RegisterWindow : Window
{
    private RegisterViewModel _viewModel;

    public string? RegisteredUsername { get; private set; }

    public RegisterWindow()
    {
        try
        {
            InitializeComponent();
            _viewModel = new RegisterViewModel();
            DataContext = _viewModel;

            _viewModel.RegisterSuccessful += OnRegisterSuccessful;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Regisztrációs ablak inicializálási hiba:\n\n{ex.Message}\n\n{ex.StackTrace}",
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

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.ConfirmPassword = passwordBox.Password;
        }
    }

    private void ViewTermsButton_Click(object sender, RoutedEventArgs e)
    {
        var termsWindow = new TermsWindow();
        termsWindow.Owner = this;
        termsWindow.ShowDialog();
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Register();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnRegisterSuccessful(object? sender, string username)
    {
        RegisteredUsername = username;
        
        // Use BeginInvoke to ensure window is fully loaded before setting DialogResult
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                DialogResult = true;
            }
            catch
            {
                // If DialogResult can't be set, just close the window
                Close();
            }
        }));
    }
}
