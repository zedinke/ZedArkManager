using Renci.SshNet;
using Renci.SshNet.Common;
using System.Text;
using ZedASAManager.Models;

namespace ZedASAManager.Services;

public class SshService : IDisposable
{
    private SshClient? _sshClient;
    private ConnectionSettings? _settings;
    private bool _disposed = false;

    public bool IsConnected => _sshClient?.IsConnected ?? false;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? ErrorReceived;
    public event EventHandler? ConnectionLost;

    public async Task<bool> ConnectAsync(ConnectionSettings settings)
    {
        _settings = settings;

        try
        {
            await Task.Run(() =>
            {
                if (_sshClient?.IsConnected == true)
                {
                    _sshClient.Disconnect();
                }

                ConnectionInfo connectionInfo;

                if (settings.UseSshKey && !string.IsNullOrEmpty(settings.SshKeyPath))
                {
                    var privateKeyFile = new PrivateKeyFile(settings.SshKeyPath);
                    connectionInfo = new ConnectionInfo(
                        settings.Host,
                        settings.Port,
                        settings.Username,
                        new PrivateKeyAuthenticationMethod(settings.Username, privateKeyFile));
                }
                else
                {
                    string password = EncryptionService.Decrypt(settings.EncryptedPassword);
                    connectionInfo = new ConnectionInfo(
                        settings.Host,
                        settings.Port,
                        settings.Username,
                        new PasswordAuthenticationMethod(settings.Username, password));
                }

                connectionInfo.Timeout = TimeSpan.FromSeconds(30);

                _sshClient = new SshClient(connectionInfo);
                _sshClient.Connect();
            });

            return _sshClient?.IsConnected ?? false;
        }
        catch (Exception ex)
        {
            OnErrorReceived($"Kapcsolódási hiba: {ex.Message}");
            return false;
        }
    }

    public async Task<string> ExecuteCommandAsync(string command)
    {
        if (_sshClient == null || !_sshClient.IsConnected)
        {
            throw new InvalidOperationException("SSH kapcsolat nincs aktív.");
        }

        try
        {
            return await Task.Run(() =>
            {
                var result = _sshClient.RunCommand(command);
                
                if (!string.IsNullOrEmpty(result.Result))
                {
                    OnOutputReceived(result.Result);
                }
                
                if (!string.IsNullOrEmpty(result.Error))
                {
                    OnErrorReceived(result.Error);
                }

                return result.Result;
            });
        }
        catch (SshConnectionException ex)
        {
            OnConnectionLost();
            throw new Exception($"SSH kapcsolat megszakadt: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            OnErrorReceived($"Parancs végrehajtási hiba: {ex.Message}");
            throw;
        }
    }

    public async Task ExecuteCommandStreamAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_sshClient == null || !_sshClient.IsConnected)
        {
            throw new InvalidOperationException("SSH kapcsolat nincs aktív.");
        }

        try
        {
            await Task.Run(() =>
            {
                using var shellStream = _sshClient.CreateShellStream("xterm", 80, 24, 800, 600, 1024);
                shellStream.WriteLine(command);
                shellStream.Expect("", TimeSpan.FromSeconds(5));

                string? line;
                while ((line = shellStream.ReadLine(TimeSpan.FromSeconds(1))) != null && !cancellationToken.IsCancellationRequested)
                {
                    OnOutputReceived(line);
                }
            }, cancellationToken);
        }
        catch (SshConnectionException ex)
        {
            OnConnectionLost();
            throw new Exception($"SSH kapcsolat megszakadt: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            OnErrorReceived($"Parancs végrehajtási hiba: {ex.Message}");
            throw;
        }
    }

    public async Task<string> ReadFileAsync(string remotePath)
    {
        if (_sshClient == null || !_sshClient.IsConnected)
        {
            throw new InvalidOperationException("SSH kapcsolat nincs aktív.");
        }

        try
        {
            return await Task.Run(() =>
            {
                string command = $"cat \"{remotePath}\"";
                var result = _sshClient.RunCommand(command);
                
                if (!string.IsNullOrEmpty(result.Error) && !result.Error.Contains("No such file"))
                {
                    OnErrorReceived(result.Error);
                }

                return result.Result;
            });
        }
        catch (SshConnectionException ex)
        {
            OnConnectionLost();
            throw new Exception($"SSH kapcsolat megszakadt: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            OnErrorReceived($"Fájl olvasási hiba: {ex.Message}");
            throw;
        }
    }

    public async Task WriteFileAsync(string remotePath, string content)
    {
        if (_sshClient == null || !_sshClient.IsConnected)
        {
            throw new InvalidOperationException("SSH kapcsolat nincs aktív.");
        }

        try
        {
            await Task.Run(() =>
            {
                // Create a temporary file with the content, then move it to the target location
                string tempFile = $"/tmp/zedasa_config_{Guid.NewGuid():N}.tmp";
                
                // Write content to temp file using base64 encoding to handle special characters
                string base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
                string writeCommand = $"echo '{base64Content}' | base64 -d > \"{tempFile}\"";
                var writeResult = _sshClient.RunCommand(writeCommand);
                
                if (!string.IsNullOrEmpty(writeResult.Error))
                {
                    throw new Exception($"Temp fájl írási hiba: {writeResult.Error}");
                }

                // Move temp file to target location
                string moveCommand = $"mv \"{tempFile}\" \"{remotePath}\"";
                var moveResult = _sshClient.RunCommand(moveCommand);
                
                if (!string.IsNullOrEmpty(moveResult.Error))
                {
                    throw new Exception($"Fájl mozgatási hiba: {moveResult.Error}");
                }
            });
        }
        catch (SshConnectionException ex)
        {
            OnConnectionLost();
            throw new Exception($"SSH kapcsolat megszakadt: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            OnErrorReceived($"Fájl írási hiba: {ex.Message}");
            throw;
        }
    }

    public SshClient? GetSshClient()
    {
        return _sshClient?.IsConnected == true ? _sshClient : null;
    }

    public void Disconnect()
    {
        try
        {
            _sshClient?.Disconnect();
        }
        catch
        {
            // Ignore disconnect errors
        }
    }

    protected virtual void OnOutputReceived(string output)
    {
        OutputReceived?.Invoke(this, output);
    }

    protected virtual void OnErrorReceived(string error)
    {
        ErrorReceived?.Invoke(this, error);
    }

    protected virtual void OnConnectionLost()
    {
        ConnectionLost?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            _sshClient?.Dispose();
            _disposed = true;
        }
    }
}
