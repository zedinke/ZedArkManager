using System.Text.RegularExpressions;
using ZedASAManager.Models;

namespace ZedASAManager.Services;

public class ServerDiscoveryService
{
    private readonly SshService _sshService;
    private string _basePath;

    public ServerDiscoveryService(SshService sshService, string? basePath = null)
    {
        _sshService = sshService;
        _basePath = basePath ?? "/home/zedinke/asa_server";
    }

    public void SetBasePath(string basePath)
    {
        _basePath = basePath;
    }

    public async Task<List<ServerInstance>> DiscoverServersAsync()
    {
        var servers = new List<ServerInstance>();

        try
        {
            // Optimized: Check POK-manager.sh and list directories in one command
            string command = $"for dir in {_basePath}/*/; do [ -f \"$dir/POK-manager.sh\" ] && echo \"$dir\"; done 2>/dev/null";
            string output = await _sshService.ExecuteCommandAsync(command);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Parallel processing of server discovery - process .env files in parallel
            var tasks = new List<Task<ServerInstance?>>();
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                    continue;

                string directoryPath = trimmedLine.TrimEnd('/');
                
                // Extract folder name from path
                string folderName = directoryPath.Contains('/') 
                    ? directoryPath.Substring(directoryPath.LastIndexOf('/') + 1) 
                    : directoryPath;
                
                string instanceName = folderName;
                string mapName = ExtractMapName(folderName);

                // Process each server's .env file in parallel
                tasks.Add(ProcessServerAsync(directoryPath, instanceName, mapName));
            }
            
            // Wait for all tasks to complete
            var results = await Task.WhenAll(tasks);
            servers.AddRange(results.Where(s => s != null)!);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Auto-discovery hiba: {ex.Message}");
        }

        return servers;
    }

    private string ExtractMapName(string folderName)
    {
        // Extract map name from folder name
        // Try to find pattern like "Rexodon-center" -> "center" or "something-mapname" -> "mapname"
        // If no pattern matches, use the folder name as map name
        var match = Regex.Match(folderName, @"(?:Rexodon-|.*-)(\w+)");
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value;
        }
        return folderName;
    }

    private async Task<ServerInstance?> ProcessServerAsync(string directoryPath, string instanceName, string mapName)
    {
        try
        {
            var server = new ServerInstance
            {
                Name = instanceName,
                DirectoryPath = directoryPath,
                MapName = mapName,
                Status = ServerStatus.Offline
            };

            // Try to read ports from .env file
            await TryLoadPortsFromEnvAsync(server);
            return server;
        }
        catch
        {
            return null;
        }
    }

    private async Task TryLoadPortsFromEnvAsync(ServerInstance server)
    {
        try
        {
            string envPath = $"{server.DirectoryPath}/.env";
            string command = $"cat {envPath}";
            string output = await _sshService.ExecuteCommandAsync(command);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("SERVER_PORT=", StringComparison.OrdinalIgnoreCase))
                {
                    var portMatch = Regex.Match(line, @"SERVER_PORT=(\d+)");
                    if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out int port))
                    {
                        server.AsaPort = port;
                    }
                }
                else if (line.StartsWith("RCON_PORT=", StringComparison.OrdinalIgnoreCase))
                {
                    var portMatch = Regex.Match(line, @"RCON_PORT=(\d+)");
                    if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out int port))
                    {
                        server.RconPort = port;
                    }
                }
            }
        }
        catch
        {
            // .env file might not exist or be readable, use defaults
            server.AsaPort = 0;
            server.RconPort = 0;
        }
    }
}
