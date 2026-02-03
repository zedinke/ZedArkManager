using System.Windows;
using System.Windows.Input;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public partial class RenameClusterWindow : Window
{
    private readonly SshService _sshService;
    private readonly ClusterInfo _cluster;
    private string _placeholderText = string.Empty;

    public RenameClusterWindow(SshService sshService, ClusterInfo cluster)
    {
        InitializeComponent();
        _sshService = sshService;
        _cluster = cluster;
        LoadLocalizedStrings();
        ClusterNameTextBox.Focus();
    }

    private void LoadLocalizedStrings()
    {
        Title = LocalizationHelper.GetString("rename_cluster");
        TitleTextBlock.Text = LocalizationHelper.GetString("rename_cluster");
        ClusterNameLabel.Text = LocalizationHelper.GetString("new_cluster_name") + ":";
        _placeholderText = LocalizationHelper.GetString("cluster_name_placeholder");
        ClusterNameTextBox.Text = _cluster.Name.Replace("Cluster_", "");
        RenameButton.Content = LocalizationHelper.GetString("rename");
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
            Rename_Click(sender, e);
        }
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        string newClusterName = ClusterNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(newClusterName))
        {
            MessageBox.Show(
                LocalizationHelper.GetString("cluster_name") + " " + LocalizationHelper.GetString("error"),
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Validate cluster name (no special characters that could cause issues)
        if (newClusterName.Contains("/") || newClusterName.Contains("\\") || newClusterName.Contains(" "))
        {
            MessageBox.Show(
                LocalizationHelper.GetString("cluster_name") + " nem tartalmazhat szóközt vagy speciális karaktereket (/ \\)",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Add Cluster_ prefix automatically
        string fullNewClusterName = newClusterName.StartsWith("Cluster_") ? newClusterName : $"Cluster_{newClusterName}";

        // If name didn't change, just close
        if (fullNewClusterName == _cluster.Name)
        {
            DialogResult = false;
            Close();
            return;
        }

        try
        {
            RenameButton.IsEnabled = false;
            RenameButton.Content = LocalizationHelper.GetString("loading") + "...";

            // Get base path
            string basePath = System.IO.Path.GetDirectoryName(_cluster.FullPath) ?? "";
            string oldPath = _cluster.FullPath;
            string newPath = $"{basePath}/{fullNewClusterName}";

            // Check if new name already exists
            string checkCommand = $"test -d \"{newPath}\" && echo 'exists' || echo 'not_exists'";
            string checkOutput = await _sshService.ExecuteCommandAsync(checkCommand);

            if (checkOutput.Contains("exists"))
            {
                MessageBox.Show(
                    LocalizationHelper.GetString("cluster_name") + " már létezik!",
                    LocalizationHelper.GetString("error"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Rename directory
            string command = $"mv \"{oldPath}\" \"{newPath}\"";
            await _sshService.ExecuteCommandAsync(command);

            MessageBox.Show(
                $"{LocalizationHelper.GetString("cluster_renamed")}: {_cluster.Name} -> {fullNewClusterName}",
                LocalizationHelper.GetString("success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{LocalizationHelper.GetString("cluster_rename_error")}: {ex.Message}",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            RenameButton.IsEnabled = true;
            RenameButton.Content = LocalizationHelper.GetString("rename");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
