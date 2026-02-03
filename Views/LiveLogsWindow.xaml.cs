using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZedASAManager.Services;
using ZedASAManager.Utilities;
using Renci.SshNet;

namespace ZedASAManager.Views;

public partial class LiveLogsWindow : Window
{
    private readonly SshService _sshService;
    private readonly string _serverDirectoryPath;
    private readonly string _instanceName;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _logTask;

    public LiveLogsWindow(SshService sshService, string serverDirectoryPath, string instanceName)
    {
        InitializeComponent();
        _sshService = sshService;
        _serverDirectoryPath = serverDirectoryPath;
        _instanceName = instanceName;
        
        LoadLocalizedStrings();
        StartLiveLogs();
    }

    private void LoadLocalizedStrings()
    {
        Title = LocalizationHelper.GetString("live_logs");
        TitleTextBlock.Text = $"{LocalizationHelper.GetString("live_logs")}: {_instanceName}";
        ClearButton.Content = LocalizationHelper.GetString("clear");
        CloseButton.Content = LocalizationHelper.GetString("close");
    }

    private void StartLiveLogs()
    {
        if (!_sshService.IsConnected)
        {
            MessageBox.Show(
                LocalizationHelper.GetString("disconnected"),
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Close();
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _logTask = Task.Run(async () => await ReadLiveLogsAsync(_cancellationTokenSource.Token));
    }

    private async Task ReadLiveLogsAsync(CancellationToken cancellationToken)
    {
        try
        {
            string command = $"cd \"{_serverDirectoryPath}\" && ./POK-manager.sh -logs -live {_instanceName}";
            
            // Use SSH shell stream for live output
            var sshClient = _sshService.GetSshClient();
            if (sshClient == null || !sshClient.IsConnected)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        LocalizationHelper.GetString("disconnected"),
                        LocalizationHelper.GetString("error"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    Close();
                });
                return;
            }

            await Task.Run(() =>
            {
                using var shellStream = sshClient.CreateShellStream("xterm", 80, 24, 800, 600, 1024);
                shellStream.WriteLine(command);
                
                // Wait a bit for command to start
                Thread.Sleep(500);

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (shellStream.DataAvailable)
                    {
                        string? line = shellStream.ReadLine(TimeSpan.FromSeconds(1));
                        if (!string.IsNullOrEmpty(line))
                        {
                            // Update UI on dispatcher thread
                            Dispatcher.Invoke(() =>
                            {
                                LogTextBox.AppendText(line + Environment.NewLine);
                                // Scroll to end
                                LogTextBox.CaretIndex = LogTextBox.Text.Length;
                                LogTextBox.ScrollToEnd();
                                // Also scroll the ScrollViewer to bottom
                                LogScrollViewer.ScrollToEnd();
                            }, DispatcherPriority.Normal);
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"{LocalizationHelper.GetString("error")}: {ex.Message}{Environment.NewLine}");
            });
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        LogTextBox.Clear();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _logTask?.Wait(TimeSpan.FromSeconds(2));
        base.OnClosing(e);
    }
}
