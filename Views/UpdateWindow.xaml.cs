using System.Windows;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public partial class UpdateWindow : Window
{
    private readonly UpdateService _updateService;
    private readonly bool _isRequired;
    private readonly string _currentVersion;
    private readonly string _latestVersion;
    private readonly string? _releaseNotes;

    public UpdateWindow(UpdateService updateService, bool isRequired, string currentVersion, string latestVersion, string? releaseNotes = null)
    {
        InitializeComponent();
        _updateService = updateService;
        _isRequired = isRequired;
        _currentVersion = currentVersion;
        _latestVersion = latestVersion;
        _releaseNotes = releaseNotes;

        LoadLocalizedStrings();
        SetupWindow();
    }

    private void LoadLocalizedStrings()
    {
        Title = _isRequired 
            ? LocalizationHelper.GetString("update_required")
            : LocalizationHelper.GetString("update_available");

        TitleTextBlock.Text = _isRequired
            ? LocalizationHelper.GetString("update_required")
            : LocalizationHelper.GetString("update_available");

        CurrentVersionTextBlock.Text = $"{LocalizationHelper.GetString("current_version")}: {_currentVersion}";
        LatestVersionTextBlock.Text = $"{LocalizationHelper.GetString("latest_version")}: {_latestVersion}";

        if (!string.IsNullOrEmpty(_releaseNotes))
        {
            ReleaseNotesLabel.Text = LocalizationHelper.GetString("release_notes");
            ReleaseNotesLabel.Visibility = Visibility.Visible;
            ReleaseNotesTextBlock.Text = _releaseNotes;
            ReleaseNotesTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            ReleaseNotesLabel.Visibility = Visibility.Collapsed;
            ReleaseNotesTextBlock.Text = LocalizationHelper.GetString("no_release_notes");
            ReleaseNotesTextBlock.Visibility = Visibility.Visible;
        }

        UpdateButton.Content = LocalizationHelper.GetString("update");
        CloseButton.Content = LocalizationHelper.GetString("close");
    }

    private void SetupWindow()
    {
        if (_isRequired)
        {
            CloseButton.Visibility = Visibility.Collapsed;
            this.Closing += (s, e) =>
            {
                if (ProgressBar.Visibility == Visibility.Visible)
                {
                    e.Cancel = true;
                }
            };
        }
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateButton.IsEnabled = false;
            StatusTextBlock.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = true;

            var progress = new Progress<string>(status =>
            {
                StatusTextBlock.Text = status;
            });

            // Download update
            var zipPath = await _updateService.DownloadUpdateAsync(_latestVersion, progress);

            // Extract and backup
            var extractFolder = await _updateService.ExtractAndBackupAsync(zipPath, progress);

            // Apply update (this will restart the application)
            await _updateService.ApplyUpdateAsync(extractFolder, progress);

            StatusTextBlock.Text = LocalizationHelper.GetString("update_success");
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"{LocalizationHelper.GetString("update_error")}: {ex.Message}";
            ProgressBar.Visibility = Visibility.Collapsed;
            UpdateButton.IsEnabled = true;
            MessageBox.Show(
                $"{LocalizationHelper.GetString("update_failed")}\n\n{ex.Message}",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRequired)
        {
            this.Close();
        }
    }
}
