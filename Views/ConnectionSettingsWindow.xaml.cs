using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace ZedASAManager.Views;

public partial class ConnectionSettingsWindow : Window
{
    public ConnectionSettingsWindow()
    {
        InitializeComponent();
        
        // Handle DataContext changes to set up PropertyChanged handler
        this.DataContextChanged += ConnectionSettingsWindow_DataContextChanged;
    }

    private void ConnectionSettingsWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ViewModels.ConnectionSettingsViewModel viewModel)
        {
            viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(ViewModels.ConnectionSettingsViewModel.UseSshKey))
                {
                    if (viewModel.UseSshKey)
                    {
                        PasswordPanel.Visibility = Visibility.Collapsed;
                        SshKeyPanel.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        PasswordPanel.Visibility = Visibility.Visible;
                        SshKeyPanel.Visibility = Visibility.Collapsed;
                    }
                }
            };
            
            // Set initial visibility
            if (viewModel.UseSshKey)
            {
                PasswordPanel.Visibility = Visibility.Collapsed;
                SshKeyPanel.Visibility = Visibility.Visible;
            }
            else
            {
                PasswordPanel.Visibility = Visibility.Visible;
                SshKeyPanel.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void BrowseSshKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SSH Key Files (*.pem;*.key)|*.pem;*.key|All Files (*.*)|*.*",
            Title = "SSH Kulcs Fájl Kiválasztása"
        };

        if (dialog.ShowDialog() == true)
        {
            if (DataContext is ViewModels.ConnectionSettingsViewModel viewModel)
            {
                viewModel.SshKeyPath = dialog.FileName;
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
