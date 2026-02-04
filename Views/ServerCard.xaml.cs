using System;
using System.IO;
using System.Windows.Controls;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public partial class ServerCard : UserControl
{
    public ServerCard()
    {
        try
        {
            LogHelper.WriteToServerCardLog("ServerCard constructor started");
            InitializeComponent();
            LogHelper.WriteToServerCardLog("ServerCard InitializeComponent completed");
        }
        catch (Exception ex)
        {
            LogHelper.WriteToServerCardLog($"ServerCard ERROR: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}");
            throw;
        }
    }

    private void ShutdownButton_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Content = LocalizationHelper.GetString("scheduled_shutdown");
        }
    }

    private void StartButton_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Content = LocalizationHelper.GetString("start");
        }
    }

    private void StopButton_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Content = LocalizationHelper.GetString("stop");
        }
    }

    private void RestartButton_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Content = LocalizationHelper.GetString("restart");
        }
    }

    private void UpdateButton_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Content = LocalizationHelper.GetString("server_update");
        }
    }

    private void ConfigButton_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Content = LocalizationHelper.GetString("configuration");
        }
    }

    private void LiveLogsButton_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Content = LocalizationHelper.GetString("live_logs");
        }
    }

    private void DeleteButton_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Content = LocalizationHelper.GetString("remove_server");
        }
    }
}
