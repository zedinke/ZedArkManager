namespace ZedASAManager.Models;

public class SavedServer
{
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? EncryptedPassword { get; set; }
    public string? SshKeyPath { get; set; }
    public bool UseSshKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
