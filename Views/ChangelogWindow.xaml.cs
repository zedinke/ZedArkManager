using System.Windows;
using ZedASAManager.ViewModels;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public partial class ChangelogWindow : Window
{
    public ChangelogWindow()
    {
        InitializeComponent();
        
        LoadLocalizedStrings();
        
        var updateService = new UpdateService();
        var viewModel = new ChangelogViewModel(updateService);
        DataContext = viewModel;
        
        Loaded += async (s, e) =>
        {
            await viewModel.LoadReleasesAsync();
        };
    }

    private void LoadLocalizedStrings()
    {
        Title = LocalizationHelper.GetString("changelog");
        TitleTextBlock.Text = LocalizationHelper.GetString("changelog");
        VersionsLabel.Text = LocalizationHelper.GetString("versions");
        RefreshButton.Content = LocalizationHelper.GetString("refresh");
        CloseButton.Content = LocalizationHelper.GetString("close");
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
