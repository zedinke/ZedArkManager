using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public class ClusterInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
}

public partial class ClusterManagementWindow : Window
{
    private readonly SshService _sshService;
    private readonly string _username;
    private readonly string _serverBasePath;
    private readonly bool _isConnected;
    private readonly ObservableCollection<ClusterInfo> _clusters = new();

    public ClusterManagementWindow(SshService sshService, string username, bool isConnected, string? serverBasePath = null)
    {
        InitializeComponent();
        _sshService = sshService;
        _username = username;
        _serverBasePath = serverBasePath ?? $"/home/{username}/asa_server";
        _isConnected = isConnected;
        ClustersItemsControl.ItemsSource = _clusters;
        LoadLocalizedStrings();
        _ = LoadClusters();
    }

    private void LoadLocalizedStrings()
    {
        Title = LocalizationHelper.GetString("cluster_management");
        TitleTextBlock.Text = LocalizationHelper.GetString("cluster_management");
        CreateClusterButton.Content = LocalizationHelper.GetString("cluster_create");
        CancelButton.Content = LocalizationHelper.GetString("cancel");
    }

    private void StackPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is StackPanel stackPanel)
        {
            var buttons = stackPanel.Children.OfType<Button>().ToList();
            if (buttons.Count >= 2)
            {
                buttons[0].Content = LocalizationHelper.GetString("rename");
                buttons[1].Content = LocalizationHelper.GetString("delete");
            }
        }
    }

    private async Task LoadClusters()
    {
        if (!_isConnected)
        {
            return;
        }

        try
        {
            // List all directories starting with Cluster_ in serverBasePath
            string basePath = _serverBasePath;
            string command = $"ls -d {basePath}/Cluster_* 2>/dev/null | sed 's|{basePath}/||'";
            string output = await _sshService.ExecuteCommandAsync(command);

            _clusters.Clear();

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var clusterName = line.Trim();
                if (!string.IsNullOrEmpty(clusterName) && clusterName.StartsWith("Cluster_"))
                {
                    _clusters.Add(new ClusterInfo
                    {
                        Name = clusterName,
                        FullPath = $"{basePath}/{clusterName}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{LocalizationHelper.GetString("error")}: {ex.Message}",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CreateCluster_Click(object sender, RoutedEventArgs e)
    {
        if (!_isConnected)
        {
            MessageBox.Show(
                LocalizationHelper.GetString("disconnected"),
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var createClusterWindow = new CreateClusterWindow(_sshService, _username, _serverBasePath);
        createClusterWindow.Owner = this;
        var result = createClusterWindow.ShowDialog();
        
        if (result == true)
        {
            _ = LoadClusters();
        }
    }

    private async void RenameCluster_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ClusterInfo cluster)
        {
            var renameWindow = new RenameClusterWindow(_sshService, cluster);
            renameWindow.Owner = this;
            var result = renameWindow.ShowDialog();
            
            if (result == true)
            {
                await LoadClusters();
            }
        }
    }

    private async void DeleteCluster_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ClusterInfo cluster)
        {
            var result = MessageBox.Show(
                $"Biztosan törölni szeretnéd a '{cluster.Name}' clustert?",
                LocalizationHelper.GetString("delete_cluster"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    string command = $"rm -rf \"{cluster.FullPath}\"";
                    await _sshService.ExecuteCommandAsync(command);

                    MessageBox.Show(
                        $"{LocalizationHelper.GetString("cluster_deleted")}: {cluster.Name}",
                        LocalizationHelper.GetString("success"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    await LoadClusters();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"{LocalizationHelper.GetString("cluster_delete_error")}: {ex.Message}",
                        LocalizationHelper.GetString("error"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
