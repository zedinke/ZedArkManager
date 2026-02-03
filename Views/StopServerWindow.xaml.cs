using System.Windows;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public partial class StopServerWindow : Window
{
    public StopServerWindow()
    {
        InitializeComponent();
        LoadLocalizedStrings();
    }

    private void LoadLocalizedStrings()
    {
        Title = LocalizationHelper.GetString("stop");
        CloseButton.Content = LocalizationHelper.GetString("close");
    }

    public void UpdateStatus(string status)
    {
        StatusTextBlock.Text = status;
    }

    public void SetCompleted()
    {
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Value = 100;
        CloseButton.IsEnabled = true;
    }

    public void SetClosable()
    {
        // Allow closing the window at any time (for scheduled shutdown)
        CloseButton.IsEnabled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
