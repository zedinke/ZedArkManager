using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZedASAManager.Models;
using ZedASAManager.Services;
using ZedASAManager.Utilities;
using Renci.SshNet;

namespace ZedASAManager.Views;

public partial class UpdateServerWindow : Window
{
    private readonly SshService _sshService;
    private readonly string _serverDirectoryPath;
    private readonly string _instanceName;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _updateTask;

    public UpdateServerWindow(SshService sshService, string serverDirectoryPath, string instanceName)
    {
        InitializeComponent();
        _sshService = sshService;
        _serverDirectoryPath = serverDirectoryPath;
        _instanceName = instanceName;
        
        Title = "Server Update";
        TitleTextBlock.Text = $"Server Update: {instanceName}";
        StatusTextBlock.Text = "Preparing update...";
    }

    public void AppendOutput(string text)
    {
        Dispatcher.Invoke(() =>
        {
            OutputTextBox.AppendText(text + Environment.NewLine);
            OutputTextBox.CaretIndex = OutputTextBox.Text.Length;
            OutputTextBox.ScrollToEnd();
            OutputScrollViewer.ScrollToEnd();
        });
    }

    public void UpdateStatus(string status)
    {
        Dispatcher.Invoke(() =>
        {
            StatusTextBlock.Text = status;
        });
    }

    public async Task StartUpdateAsync(ConnectionSettings? connectionSettings)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _updateTask = Task.Run(async () => await ExecuteUpdateAsync(connectionSettings, _cancellationTokenSource.Token));
    }

    private async Task ExecuteUpdateAsync(ConnectionSettings? connectionSettings, CancellationToken cancellationToken)
    {
        try
        {
            UpdateStatus("Stopping server...");
            AppendOutput("Stopping server...");
            
            // Stop server using the same logic as ExecuteStopAsync
            string stopCommand;
            string sudoPasswordPart = string.Empty;
            
            if (connectionSettings != null && !connectionSettings.UseSshKey && !string.IsNullOrEmpty(connectionSettings.EncryptedPassword))
            {
                try
                {
                    string password = EncryptionService.Decrypt(connectionSettings.EncryptedPassword);
                    if (!string.IsNullOrEmpty(password))
                    {
                        string escapedPassword = password.Replace("'", "'\\''");
                        sudoPasswordPart = $"echo '{escapedPassword}' | sudo -S bash -c \"cd {_serverDirectoryPath} && ./POK-manager.sh -stop {_instanceName}\"";
                    }
                }
                catch { }
            }
            
            if (string.IsNullOrEmpty(sudoPasswordPart))
            {
                stopCommand = $"cd {_serverDirectoryPath} && (sudo -n ./POK-manager.sh -stop {_instanceName} 2>&1 || ./POK-manager.sh -stop {_instanceName})";
            }
            else
            {
                stopCommand = sudoPasswordPart;
            }
            
            string stopResult = await _sshService.ExecuteCommandAsync(stopCommand);
            AppendOutput(stopResult);
            
            UpdateStatus("Waiting 15 seconds...");
            AppendOutput("Waiting 15 seconds before update...");
            for (int i = 15; i > 0; i--)
            {
                if (cancellationToken.IsCancellationRequested) return;
                UpdateStatus($"Waiting {i} seconds...");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            
            UpdateStatus("Running update command...");
            AppendOutput("Starting update...");
            
            // Run update command with live output
            string updateCommand;
            if (string.IsNullOrEmpty(sudoPasswordPart))
            {
                updateCommand = $"cd {_serverDirectoryPath} && ./POK-manager.sh -update {_instanceName}";
            }
            else
            {
                // For update, we need to use sudo with password
                string password = EncryptionService.Decrypt(connectionSettings!.EncryptedPassword);
                string escapedPassword = password.Replace("'", "'\\''");
                updateCommand = $"echo '{escapedPassword}' | sudo -S bash -c \"cd {_serverDirectoryPath} && ./POK-manager.sh -update {_instanceName}\"";
            }
            
            // Execute update command with live streaming
            var sshClient = _sshService.GetSshClient();
            if (sshClient != null && sshClient.IsConnected)
            {
                await Task.Run(() =>
                {
                    using var shellStream = sshClient.CreateShellStream("xterm", 80, 24, 800, 600, 1024);
                    shellStream.WriteLine(updateCommand);
                    
                    Thread.Sleep(500);
                    
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (shellStream.DataAvailable)
                        {
                            string? line = shellStream.ReadLine(TimeSpan.FromSeconds(1));
                            if (!string.IsNullOrEmpty(line))
                            {
                                AppendOutput(line);
                            }
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                }, cancellationToken);
            }
            
            UpdateStatus("Update completed. Waiting 10 minutes before restart...");
            AppendOutput("Update completed. Waiting 10 minutes before restart...");
            
            // Wait 10 minutes
            for (int i = 600; i > 0; i--)
            {
                if (cancellationToken.IsCancellationRequested) return;
                int minutes = i / 60;
                int seconds = i % 60;
                UpdateStatus($"Waiting {minutes}:{seconds:D2} before restart...");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            
            UpdateStatus("Starting server...");
            AppendOutput("Starting server...");
            
            // Start server using the same logic as ExecuteActionAsync("start")
            string startCommand;
            if (connectionSettings != null && !connectionSettings.UseSshKey && !string.IsNullOrEmpty(connectionSettings.EncryptedPassword))
            {
                try
                {
                    string password = EncryptionService.Decrypt(connectionSettings.EncryptedPassword);
                    if (!string.IsNullOrEmpty(password))
                    {
                        string escapedPassword = password.Replace("'", "'\\''");
                        startCommand = $"echo '{escapedPassword}' | sudo -S bash -c \"cd {_serverDirectoryPath} && yes | ./POK-manager.sh -start {_instanceName}\"";
                    }
                    else
                    {
                        startCommand = $"cd {_serverDirectoryPath} && (yes | sudo -n ./POK-manager.sh -start {_instanceName} 2>&1 || yes | ./POK-manager.sh -start {_instanceName})";
                    }
                }
                catch
                {
                    startCommand = $"cd {_serverDirectoryPath} && (yes | sudo -n ./POK-manager.sh -start {_instanceName} 2>&1 || yes | ./POK-manager.sh -start {_instanceName})";
                }
            }
            else
            {
                startCommand = $"cd {_serverDirectoryPath} && (yes | sudo -n ./POK-manager.sh -start {_instanceName} 2>&1 || yes | ./POK-manager.sh -start {_instanceName})";
            }
            
            string startResult = await _sshService.ExecuteCommandAsync(startCommand);
            AppendOutput(startResult);
            
            UpdateStatus("Server update completed!");
            AppendOutput("Server update completed successfully!");
        }
        catch (Exception ex)
        {
            AppendOutput($"Error: {ex.Message}");
            UpdateStatus($"Error: {ex.Message}");
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _updateTask?.Wait(TimeSpan.FromSeconds(2));
        base.OnClosing(e);
    }
}
