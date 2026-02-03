namespace ZedASAManager.Models;

public class ServerInstance
{
    public string Name { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public int AsaPort { get; set; }
    public int RconPort { get; set; }
    public ServerStatus Status { get; set; } = ServerStatus.Offline;
    public double CpuUsage { get; set; }
    public string MemoryUsage { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public int OnlinePlayers { get; set; }
    public int MaxPlayers { get; set; }
}
