namespace ZedASAManager.Models;

public class ConnectionSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string? SshKeyPath { get; set; }
    public bool UseSshKey { get; set; } = false;
    public string ServerBasePath { get; set; } = string.Empty;
}
