using System.Windows;
using System.Windows.Input;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public partial class CreateClusterWindow : Window
{
    private readonly SshService _sshService;
    private readonly string _username;
    private readonly string _serverBasePath;

    public CreateClusterWindow(SshService sshService, string username, string? serverBasePath = null)
    {
        InitializeComponent();
        _sshService = sshService;
        _username = username;
        _serverBasePath = serverBasePath ?? $"/home/{username}/asa_server";
        LoadLocalizedStrings();
        ClusterNameTextBox.Focus();
    }

    private string _placeholderText = string.Empty;

    private void LoadLocalizedStrings()
    {
        Title = LocalizationHelper.GetString("cluster_create");
        TitleTextBlock.Text = LocalizationHelper.GetString("cluster_create");
        ClusterNameLabel.Text = LocalizationHelper.GetString("cluster_name") + ":";
        _placeholderText = LocalizationHelper.GetString("cluster_name_placeholder");
        ClusterNameTextBox.Text = _placeholderText;
        ClusterNameTextBox.Foreground = System.Windows.Media.Brushes.Gray;
        CreateButton.Content = LocalizationHelper.GetString("create");
        CancelButton.Content = LocalizationHelper.GetString("cancel");
    }

    private void ClusterNameTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (ClusterNameTextBox.Text == _placeholderText)
        {
            ClusterNameTextBox.Text = string.Empty;
            ClusterNameTextBox.Foreground = System.Windows.Media.Brushes.Black;
        }
    }

    private void ClusterNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Create_Click(sender, e);
        }
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        string clusterName = ClusterNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(clusterName))
        {
            MessageBox.Show(
                LocalizationHelper.GetString("cluster_name") + " " + LocalizationHelper.GetString("error"),
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Check if placeholder text is still there
        if (clusterName == _placeholderText || string.IsNullOrWhiteSpace(clusterName))
        {
            MessageBox.Show(
                LocalizationHelper.GetString("cluster_name") + " " + LocalizationHelper.GetString("error"),
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Validate cluster name (no special characters that could cause issues)
        if (clusterName.Contains("/") || clusterName.Contains("\\") || clusterName.Contains(" "))
        {
            MessageBox.Show(
                LocalizationHelper.GetString("cluster_name") + " nem tartalmazhat szóközt vagy speciális karaktereket (/ \\)",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            CreateButton.IsEnabled = false;
            CreateButton.Content = LocalizationHelper.GetString("loading") + "...";

            // Add Cluster_ prefix automatically
            string fullClusterName = clusterName.StartsWith("Cluster_") ? clusterName : $"Cluster_{clusterName}";

            // Create directory: {serverBasePath}/Cluster_{Cluster-neve}
            string directoryPath = $"{_serverBasePath}/{fullClusterName}";
            string command = $"mkdir -p \"{directoryPath}\"";

            await _sshService.ExecuteCommandAsync(command);

            // Generate 64 character Cluster ID
            string clusterId = GenerateClusterId();
            string clusterIdFilePath = $"{directoryPath}/cluster_id.txt";
            await _sshService.WriteFileAsync(clusterIdFilePath, clusterId);

            MessageBox.Show(
                $"{LocalizationHelper.GetString("cluster_created")}: {directoryPath}",
                LocalizationHelper.GetString("success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{LocalizationHelper.GetString("cluster_create_error")}: {ex.Message}",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            CreateButton.IsEnabled = true;
            CreateButton.Content = LocalizationHelper.GetString("create");
        }
    }

    private string GenerateClusterId()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var clusterId = new char[64];
        
        for (int i = 0; i < 64; i++)
        {
            clusterId[i] = chars[random.Next(chars.Length)];
        }
        
        return new string(clusterId);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
