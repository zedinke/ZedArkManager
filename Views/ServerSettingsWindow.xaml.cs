using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using ZedASAManager.Models;
using ZedASAManager.Services;

namespace ZedASAManager.Views;

public partial class ServerSettingsWindow : Window
{
    public SavedServer? SavedServer { get; private set; }

    public ServerSettingsWindow()
    {
        InitializeComponent();
    }

    private void UseSshKeyCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        SshKeyPanel.Visibility = Visibility.Visible;
        // Password panel stays visible - user can use either password or key
    }

    private void UseSshKeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        SshKeyPanel.Visibility = Visibility.Collapsed;
    }

    private void BrowseSshKey_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "SSH Key files (*.pem;*.key)|*.pem;*.key|All files (*.*)|*.*",
            Title = "SSH kulcs fájl kiválasztása"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            SshKeyPathTextBox.Text = openFileDialog.FileName;
        }
    }

    private async void GenerateSshKey_Click(object sender, RoutedEventArgs e)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(HostTextBox.Text) ||
            string.IsNullOrWhiteSpace(UsernameTextBox.Text) ||
            string.IsNullOrEmpty(PasswordBox.Password))
        {
            MessageBox.Show(
                "Kérjük, töltse ki a Host, Felhasználónév és Jelszó mezőket az SSH kulcs generálásához!",
                "Hiányzó adatok",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(PortTextBox.Text, out int port) || port <= 0 || port > 65535)
        {
            MessageBox.Show(
                "Érvénytelen port szám! (1-65535)",
                "Hiba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        // Disable button during generation
        if (sender is System.Windows.Controls.Button btn)
        {
            btn.IsEnabled = false;
            btn.Content = "Generálás...";
        }

        try
        {
            string host = HostTextBox.Text.Trim();
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;
            string keyName = $"zedasa_{username}_{host.Replace(".", "_")}";

            var (privateKeyPath, publicKey, success, errorMessage) = await SshKeyService.GenerateAndInstallSshKeyAsync(
                host, port, username, password, keyName);

            if (success)
            {
                // Set the generated key path
                SshKeyPathTextBox.Text = privateKeyPath;
                UseSshKeyCheckBox.IsChecked = true;

                MessageBox.Show(
                    $"SSH kulcs sikeresen generálva és telepítve!\n\nPrivát kulcs: {privateKeyPath}\n\nA kulcs automatikusan be van állítva.",
                    "Sikeres",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"SSH kulcs generálása sikertelen:\n\n{errorMessage}",
                    "Hiba",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Hiba az SSH kulcs generálása során:\n\n{ex.Message}",
                "Hiba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            // Re-enable button
            if (sender is System.Windows.Controls.Button button)
            {
                button.IsEnabled = true;
                button.Content = "SSH kulcs generálása és telepítése";
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text) ||
            string.IsNullOrWhiteSpace(HostTextBox.Text) ||
            string.IsNullOrWhiteSpace(UsernameTextBox.Text))
        {
            MessageBox.Show(
                "Kérjük, töltse ki az összes kötelező mezőt!",
                "Hiányzó adatok",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(PortTextBox.Text, out int port) || port <= 0 || port > 65535)
        {
            MessageBox.Show(
                "Érvénytelen port szám! (1-65535)",
                "Hiba",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        bool useSshKey = UseSshKeyCheckBox.IsChecked == true;

        // Allow saving with either password OR SSH key (or both)
        if (!useSshKey && string.IsNullOrEmpty(PasswordBox.Password))
        {
            MessageBox.Show(
                "Kérjük, adjon meg jelszót vagy válasszon SSH kulcsot!",
                "Hiányzó adatok",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (useSshKey && string.IsNullOrWhiteSpace(SshKeyPathTextBox.Text))
        {
            MessageBox.Show(
                "Kérjük, válasszon SSH kulcs fájlt vagy generáljon egyet!",
                "Hiányzó adatok",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        SavedServer = new SavedServer
        {
            Name = NameTextBox.Text.Trim(),
            Host = HostTextBox.Text.Trim(),
            Port = port,
            Username = UsernameTextBox.Text.Trim(),
            EncryptedPassword = useSshKey ? null : EncryptionService.Encrypt(PasswordBox.Password),
            SshKeyPath = useSshKey ? SshKeyPathTextBox.Text.Trim() : null,
            UseSshKey = useSshKey,
            CreatedAt = DateTime.Now
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
