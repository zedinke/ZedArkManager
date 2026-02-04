using System.Collections.ObjectModel;
using System.Windows.Input;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.ViewModels;

public class ChangelogViewModel : ViewModelBase
{
    private readonly UpdateService _updateService;
    private ObservableCollection<UpdateService.ReleaseInfo> _releases = new();
    private UpdateService.ReleaseInfo? _selectedRelease;
    private bool _isLoading;

    public ChangelogViewModel(UpdateService updateService)
    {
        _updateService = updateService;
        LoadReleasesCommand = new RelayCommand(async () => await LoadReleasesAsync());
    }

    public ObservableCollection<UpdateService.ReleaseInfo> Releases
    {
        get => _releases;
        set => SetProperty(ref _releases, value);
    }

    public UpdateService.ReleaseInfo? SelectedRelease
    {
        get => _selectedRelease;
        set => SetProperty(ref _selectedRelease, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string FormattedPublishedDate
    {
        get
        {
            if (SelectedRelease == null || string.IsNullOrEmpty(SelectedRelease.PublishedAt))
                return "-";
            
            if (DateTime.TryParse(SelectedRelease.PublishedAt, out DateTime date))
            {
                return date.ToString("yyyy-MM-dd");
            }
            return SelectedRelease.PublishedAt;
        }
    }

    public ICommand LoadReleasesCommand { get; }

    public async Task LoadReleasesAsync()
    {
        IsLoading = true;
        try
        {
            var releases = await _updateService.GetAllReleasesAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Releases.Clear();
                foreach (var release in releases)
                {
                    Releases.Add(release);
                }
                
                // Select first release by default
                if (Releases.Count > 0)
                {
                    SelectedRelease = Releases[0];
                    OnPropertyChanged(nameof(FormattedPublishedDate));
                }
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"{LocalizationHelper.GetString("error")}: {ex.Message}",
                LocalizationHelper.GetString("error"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(SelectedRelease))
        {
            OnPropertyChanged(nameof(FormattedPublishedDate));
        }
    }
}
