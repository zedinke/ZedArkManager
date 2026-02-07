using System;
using System.Diagnostics;
using System.Linq;
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
            // First, check if the container exists
            string containerName = $"asa_{_instanceName}";
            string checkContainerCommand = $"docker ps -a --filter 'name=^{containerName}$' --format '{{{{.Names}}}}' 2>/dev/null || sudo docker ps -a --filter 'name=^{containerName}$' --format '{{{{.Names}}}}' 2>/dev/null || echo ''";
            
            string containerCheck = await _sshService.ExecuteCommandAsync(checkContainerCommand);
            
            if (string.IsNullOrWhiteSpace(containerCheck) || !containerCheck.Trim().Contains(containerName))
            {
                Dispatcher.Invoke(() =>
                {
                    LogTextBox.Text = $"⚠️ {LocalizationHelper.GetString("error")}: Container '{containerName}' not found.\n\n";
                    LogTextBox.Text += $"The server instance '{_instanceName}' has not been started yet or the container was removed.\n\n";
                    LogTextBox.Text += $"Please start the server first using the Start button.\n";
                    LogTextBox.CaretIndex = LogTextBox.Text.Length;
                    LogTextBox.ScrollToEnd();
                });
                return;
            }
            
            // Check if container is running
            string checkRunningCommand = $"docker ps --filter 'name=^{containerName}$' --format '{{{{.Names}}}}' 2>/dev/null || sudo docker ps --filter 'name=^{containerName}$' --format '{{{{.Names}}}}' 2>/dev/null || echo ''";
            string runningCheck = await _sshService.ExecuteCommandAsync(checkRunningCommand);
            
            if (string.IsNullOrWhiteSpace(runningCheck) || !runningCheck.Trim().Contains(containerName))
            {
                Dispatcher.Invoke(() =>
                {
                    LogTextBox.Text = $"⚠️ Container '{containerName}' exists but is not running.\n\n";
                    LogTextBox.Text += $"The server instance '{_instanceName}' is currently stopped.\n\n";
                    LogTextBox.Text += $"Please start the server first using the Start button.\n";
                    LogTextBox.CaretIndex = LogTextBox.Text.Length;
                    LogTextBox.ScrollToEnd();
                });
                return;
            }
            
            // First, get only the last 200 lines of existing logs, then start live streaming
            // Try to find log files in the server directory
            string initialCommand = $"cd \"{_serverDirectoryPath}\" && find {_instanceName}/logs -name '*.log' -type f 2>/dev/null | head -1 | xargs tail -n 200 2>/dev/null || echo ''";
            string liveCommand = $"cd \"{_serverDirectoryPath}\" && ./POK-manager.sh -logs -live {_instanceName}";

            // First, load only the last 200 lines of existing logs asynchronously
            try
            {
                string initialOutput = await _sshService.ExecuteCommandAsync(initialCommand);
                if (!string.IsNullOrEmpty(initialOutput))
                {
                    var initialLines = initialOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var last200Lines = initialLines.Length > 200 
                        ? initialLines.Skip(initialLines.Length - 200).ToArray()
                        : initialLines;
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (last200Lines.Length > 0)
                        {
                            LogTextBox.Text = string.Join(Environment.NewLine, last200Lines) + Environment.NewLine;
                            LogTextBox.CaretIndex = LogTextBox.Text.Length;
                            LogTextBox.ScrollToEnd();
                            LogScrollViewer.ScrollToEnd();
                        }
                    });
                }
            }
            catch
            {
                // If initial load fails, just start with empty log
            }

            // Now start live streaming with stateless SSH connection
            using var shellStreamWrapper = await _sshService.CreateShellStreamAsync();
            var shellStream = shellStreamWrapper.ShellStream;
            
            shellStream.WriteLine(liveCommand);
            
            // Wait a bit for command to start
            Thread.Sleep(500);

            while (!cancellationToken.IsCancellationRequested)
            {
                if (shellStreamWrapper.IsConnected && shellStream.DataAvailable)
                {
                    string? line = shellStream.ReadLine(TimeSpan.FromSeconds(1));
                    if (!string.IsNullOrEmpty(line))
                    {
                        // Update UI on dispatcher thread
                        Dispatcher.Invoke(() =>
                        {
                            LogTextBox.AppendText(line + Environment.NewLine);
                            
                            // Limit to last 200 lines
                            var lines = LogTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                            if (lines.Length > 200)
                            {
                                var last200Lines = string.Join(Environment.NewLine, lines.Skip(lines.Length - 200));
                                LogTextBox.Text = last200Lines;
                            }
                            
                            // Scroll to end
                            LogTextBox.CaretIndex = LogTextBox.Text.Length;
                            LogTextBox.ScrollToEnd();
                            // Also scroll the ScrollViewer to bottom
                            LogScrollViewer.ScrollToEnd();
                        }, DispatcherPriority.Normal);
                    }
                }
                else if (!shellStreamWrapper.IsConnected)
                {
                    // Connection lost, try to reconnect
                    Dispatcher.Invoke(() =>
                    {
                        LogTextBox.AppendText($"{LocalizationHelper.GetString("disconnected")}{Environment.NewLine}");
                    });
                    break;
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
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
