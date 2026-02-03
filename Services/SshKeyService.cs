using System.Diagnostics;
using System.IO;
using System.Text;
using Renci.SshNet;
using ZedASAManager.Models;

namespace ZedASAManager.Services;

public class SshKeyService
{
    public static async Task<(string privateKeyPath, string publicKey, bool success, string errorMessage)> GenerateAndInstallSshKeyAsync(
        string host, 
        int port, 
        string username, 
        string password,
        string? keyName = null)
    {
        try
        {
            // Generate key name if not provided
            if (string.IsNullOrEmpty(keyName))
            {
                keyName = $"zedasa_{username}_{host.Replace(".", "_")}";
            }

            // Get user's .ssh directory
            string sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
            if (!Directory.Exists(sshDir))
            {
                Directory.CreateDirectory(sshDir);
            }

            string privateKeyPath = Path.Combine(sshDir, $"{keyName}");
            string publicKeyPath = Path.Combine(sshDir, $"{keyName}.pub");

            // Check if key already exists
            if (File.Exists(privateKeyPath))
            {
                // Read existing public key
                string existingPublicKey = await File.ReadAllTextAsync(publicKeyPath);
                return (privateKeyPath, existingPublicKey, false, "Az SSH kulcs már létezik ezen a néven!");
            }

            // Try to use ssh-keygen if available (Windows 10+ or Git Bash)
            bool generated = false;
            string publicKey = string.Empty;

            // Try ssh-keygen first (most reliable)
            string sshKeygenPath = FindSshKeygen();
            if (!string.IsNullOrEmpty(sshKeygenPath))
            {
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = sshKeygenPath,
                        Arguments = $"-t rsa -b 2048 -f \"{privateKeyPath}\" -N \"\" -C \"{username}@{host}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(processInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        if (process.ExitCode == 0 && File.Exists(publicKeyPath))
                        {
                            publicKey = await File.ReadAllTextAsync(publicKeyPath);
                            publicKey = publicKey.Trim();
                            generated = true;
                        }
                    }
                }
                catch { }
            }

            // Fallback: Use SSH.NET to generate key via server (less secure but works)
            if (!generated)
            {
                // Connect to server with password and generate key there, then download
                var connectionInfo = new ConnectionInfo(
                    host, port, username,
                    new PasswordAuthenticationMethod(username, password));
                connectionInfo.Timeout = TimeSpan.FromSeconds(30);

                using var client = new SshClient(connectionInfo);
                client.Connect();

                try
                {
                    // Generate key on server
                    var genCommand = client.RunCommand($"ssh-keygen -t rsa -b 2048 -f ~/.ssh/{keyName} -N '' -C '{username}@{host}'");
                    
                    // Download private key
                    using var sftp = new Renci.SshNet.SftpClient(connectionInfo);
                    sftp.Connect();
                    try
                    {
                        using var stream = File.Create(privateKeyPath);
                        sftp.DownloadFile($"~/.ssh/{keyName}", stream);
                        
                        // Download public key
                        using var pubStream = File.Create(publicKeyPath);
                        sftp.DownloadFile($"~/.ssh/{keyName}.pub", pubStream);
                        
                        publicKey = await File.ReadAllTextAsync(publicKeyPath);
                        publicKey = publicKey.Trim();
                        generated = true;
                    }
                    finally
                    {
                        sftp.Disconnect();
                    }
                }
                finally
                {
                    client.Disconnect();
                }
            }

            if (!generated || string.IsNullOrEmpty(publicKey))
            {
                return (string.Empty, string.Empty, false, "Az SSH kulcs generálása sikertelen volt. Kérjük, telepítse az OpenSSH-t vagy használjon manuális kulcsot!");
            }

            // Install public key on server (if not already installed)
            bool installed = await InstallPublicKeyOnServerAsync(host, port, username, password, publicKey);

            if (!installed)
            {
                // Clean up local files if installation failed
                try
                {
                    if (File.Exists(privateKeyPath)) File.Delete(privateKeyPath);
                    if (File.Exists(publicKeyPath)) File.Delete(publicKeyPath);
                }
                catch { }

                return (string.Empty, string.Empty, false, "A public key telepítése a szerverre sikertelen volt!");
            }

            // Verify the key works
            bool verified = await VerifySshKeyAsync(host, port, username, privateKeyPath);

            if (!verified)
            {
                return (privateKeyPath, publicKey, false, "Az SSH kulcs telepítve, de az ellenőrzés sikertelen volt. Kérjük, próbálja meg manuálisan!");
            }

            return (privateKeyPath, publicKey, true, string.Empty);
        }
        catch (Exception ex)
        {
            return (string.Empty, string.Empty, false, $"Hiba az SSH kulcs generálása során: {ex.Message}");
        }
    }

    private static string FindSshKeygen()
    {
        // Check common locations
        string[] paths = {
            "ssh-keygen", // In PATH
            @"C:\Windows\System32\OpenSSH\ssh-keygen.exe",
            @"C:\Program Files\Git\usr\bin\ssh-keygen.exe",
            @"C:\Program Files (x86)\Git\usr\bin\ssh-keygen.exe"
        };

        foreach (var path in paths)
        {
            try
            {
                if (path == "ssh-keygen")
                {
                    // Check if it's in PATH
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "where.exe",
                        Arguments = "ssh-keygen",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(processInfo);
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                        {
                            return output.Split('\n')[0].Trim();
                        }
                    }
                }
                else if (File.Exists(path))
                {
                    return path;
                }
            }
            catch { }
        }

        return string.Empty;
    }

    private static async Task<bool> InstallPublicKeyOnServerAsync(
        string host, 
        int port, 
        string username, 
        string password,
        string publicKey)
    {
        try
        {
            return await Task.Run(() =>
            {
                var connectionInfo = new ConnectionInfo(
                    host,
                    port,
                    username,
                    new PasswordAuthenticationMethod(username, password));

                connectionInfo.Timeout = TimeSpan.FromSeconds(30);

                using var client = new SshClient(connectionInfo);
                client.Connect();

                try
                {
                    // Ensure .ssh directory exists
                    client.RunCommand("mkdir -p ~/.ssh");
                    client.RunCommand("chmod 700 ~/.ssh");

                    // Check if authorized_keys exists
                    var checkCommand = client.RunCommand("test -f ~/.ssh/authorized_keys && echo 'exists' || echo 'not_exists'");
                    bool keyExists = checkCommand.Result.Trim() == "exists";

                    // Add public key to authorized_keys
                    string command;
                    if (keyExists)
                    {
                        // Check if key already exists
                        var grepCommand = client.RunCommand($"grep -F '{publicKey.Split(' ')[1]}' ~/.ssh/authorized_keys || echo 'not_found'");
                        if (grepCommand.Result.Trim() != "not_found")
                        {
                            return true; // Key already exists
                        }
                        command = $"echo '{publicKey}' >> ~/.ssh/authorized_keys";
                    }
                    else
                    {
                        command = $"echo '{publicKey}' > ~/.ssh/authorized_keys";
                    }

                    client.RunCommand(command);
                    client.RunCommand("chmod 600 ~/.ssh/authorized_keys");

                    return true;
                }
                finally
                {
                    if (client.IsConnected)
                    {
                        client.Disconnect();
                    }
                }
            });
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> VerifySshKeyAsync(
        string host, 
        int port, 
        string username,
        string privateKeyPath)
    {
        try
        {
            return await Task.Run(() =>
            {
                var privateKeyFile = new PrivateKeyFile(privateKeyPath);
                var connectionInfo = new ConnectionInfo(
                    host,
                    port,
                    username,
                    new PrivateKeyAuthenticationMethod(username, privateKeyFile));

                connectionInfo.Timeout = TimeSpan.FromSeconds(10);

                using var client = new SshClient(connectionInfo);
                client.Connect();

                try
                {
                    // Simple test command
                    var result = client.RunCommand("echo 'SSH key verified'");
                    return result.Result.Contains("SSH key verified");
                }
                finally
                {
                    if (client.IsConnected)
                    {
                        client.Disconnect();
                    }
                }
            });
        }
        catch
        {
            return false;
        }
    }
}
