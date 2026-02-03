using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using ZedASAManager.Models;

namespace ZedASAManager.Services;

public class MonitoringService : IDisposable
{
    private readonly SshService _sshService;
    private ConnectionSettings? _connectionSettings;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning = false;
    private readonly ConcurrentDictionary<string, ServerStats> _serverStats = new();
    private readonly ConcurrentDictionary<string, string> _serverDirectoryPaths = new();
    private readonly ConcurrentDictionary<string, string> _cachedInstanceNames = new(); 

    public event EventHandler<ServerStatsEventArgs>? ServerStatsUpdated;
    public event EventHandler<string>? StatusOutputReceived;

    public class ServerStatsEventArgs : EventArgs
    {
        public string ServerName { get; set; } = string.Empty;
        public ServerStatus Status { get; set; }
        public double CpuUsage { get; set; }
        public string MemoryUsage { get; set; } = string.Empty;
        public double DiskUsagePercent { get; set; }
        public double NetworkRxMbps { get; set; }
        public double NetworkTxMbps { get; set; }
        public double SystemCpuUsage { get; set; }
        public double SystemMemoryUsagePercent { get; set; }
        public int OnlinePlayers { get; set; }
        public int MaxPlayers { get; set; }
        public int GameDay { get; set; }
        public string ServerVersion { get; set; } = string.Empty;
        public string ServerPing { get; set; } = string.Empty;
    }

    public class ServerStats
    {
        public ServerStatus Status { get; set; }
        public double CpuUsage { get; set; }
        public string MemoryUsage { get; set; } = string.Empty;
        public double DiskUsagePercent { get; set; }
        public double NetworkRxMbps { get; set; }
        public double NetworkTxMbps { get; set; }
        public double SystemCpuUsage { get; set; }
        public double SystemMemoryUsagePercent { get; set; }
        public int OnlinePlayers { get; set; }
        public int MaxPlayers { get; set; }
        public int GameDay { get; set; }
        public string ServerVersion { get; set; } = string.Empty;
        public string ServerPing { get; set; } = string.Empty;
    }

    public MonitoringService(SshService sshService)
    {
        _sshService = sshService;
    }

    public void SetConnectionSettings(ConnectionSettings? settings)
    {
        _connectionSettings = settings;
    }

    public void Start()
    {
        if (_isRunning)
        {
            System.Diagnostics.Debug.WriteLine("[MonitoringService] Already running");
            return;
        }
        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(2)); // 2 másodperc elég, hogy ne terhelje az SSH-t
        System.Diagnostics.Debug.WriteLine("[MonitoringService] Starting monitoring loop");
        OnStatusOutputReceived("[MonitoringService] Starting monitoring...");
        _ = Task.Run(async () => await MonitorLoopAsync(_cancellationTokenSource.Token));
    }

    public void Stop()
    {
        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        _timer?.Dispose();
        _timer = null;
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                if (_sshService.IsConnected)
                {
                    // Előbb a státusz, utána a statisztika
                    await UpdateServerStatusesAsync();
                    await UpdateServerStatsAsync();
                    await UpdateSystemStatsAsync();
                    await UpdatePlayerCountsAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monitoring hiba: {ex.Message}");
            }

            if (_timer != null) await _timer.WaitForNextTickAsync(cancellationToken);
            else break;
        }
    }

    private async Task UpdateServerStatusesAsync()
    {
        try
        {
            if (_serverStats.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[MonitoringService] No servers registered, skipping status update");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MonitoringService] Updating status for {_serverStats.Count} server(s)");
            System.Diagnostics.Debug.WriteLine($"[MonitoringService] Registered servers: {string.Join(", ", _serverStats.Keys)}");

            // Process servers in parallel for better performance
            var serverNames = _serverStats.Keys.ToList();
            var tasks = serverNames.Select(async serverName =>
            {
                var stats = _serverStats[serverName];
                
                System.Diagnostics.Debug.WriteLine($"[MonitoringService] Processing server: '{serverName}'");
                
                // Get directory path for this server
                if (!_serverDirectoryPaths.TryGetValue(serverName, out string? directoryPath) || string.IsNullOrEmpty(directoryPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[{serverName}] No directory path, marking offline");
                    OnStatusOutputReceived($"[{serverName}] ERROR: No directory path found");
                    stats.Status = ServerStatus.Offline;
                    stats.CpuUsage = 0;
                    stats.MemoryUsage = string.Empty;
                    stats.OnlinePlayers = 0;
                    stats.MaxPlayers = 0;
                    stats.GameDay = 0;
                    stats.ServerVersion = string.Empty;
                    stats.ServerPing = string.Empty;
                    OnServerStatsUpdated(serverName, stats);
                    return;
                }

                string instanceName = await GetOrCacheInstanceName(serverName);
                string containerName = $"asa_{instanceName}";
                
                System.Diagnostics.Debug.WriteLine($"[{serverName}] Checking - instanceName='{instanceName}', containerName='{containerName}', directoryPath='{directoryPath}'");
                OnStatusOutputReceived($"[{serverName}] Checking status - instance: {instanceName}");
                
                // 1. Use POK-manager.sh -status to check if server is up
                // Command: sudo ./POK-manager.sh -status Instance_servermappaneve (Instance_ nélkül, csak a servermappaneve)
                string statusCmd = string.Empty;
                string sudoPasswordPart = string.Empty;
                
                // Try to get password for sudo
                if (_connectionSettings != null && !_connectionSettings.UseSshKey && !string.IsNullOrEmpty(_connectionSettings.EncryptedPassword))
                {
                    try
                    {
                        string password = EncryptionService.Decrypt(_connectionSettings.EncryptedPassword);
                        if (!string.IsNullOrEmpty(password))
                        {
                            // Escape password for shell
                            string escapedPassword = password.Replace("'", "'\\''");
                            // Use echo to pipe password to sudo -S
                            sudoPasswordPart = $"echo '{escapedPassword}' | sudo -S bash -c \"cd {directoryPath} && ./POK-manager.sh -status {instanceName} 2>&1\"";
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{serverName}] Password decryption failed: {ex.Message}");
                        OnStatusOutputReceived($"[{serverName}] WARNING: Password decryption failed");
                    }
                }
                
                // Use sudo -n first (non-interactive, requires sudoers config), fallback to sudo with password or without sudo
                if (string.IsNullOrEmpty(sudoPasswordPart))
                {
                    // Try sudo -n first, then fallback to without sudo
                    statusCmd = $"cd \"{directoryPath}\" && (sudo -n ./POK-manager.sh -status {instanceName} 2>&1 || ./POK-manager.sh -status {instanceName} 2>&1)";
                }
                else
                {
                    statusCmd = sudoPasswordPart;
                }
                
                System.Diagnostics.Debug.WriteLine($"[{serverName}] Executing command: {statusCmd}");
                
                try
                {
                    string statusOutput = await _sshService.ExecuteCommandAsync(statusCmd);
                    System.Diagnostics.Debug.WriteLine($"[{serverName}] POK-manager.sh -status output: '{statusOutput}'");
                    
                    // Log status output for visibility
                    if (string.IsNullOrWhiteSpace(statusOutput))
                    {
                        OnStatusOutputReceived($"[{serverName}] Status output: (empty)");
                    }
                    else
                    {
                        OnStatusOutputReceived($"[{serverName}] Status output: {statusOutput.Trim()}");
                    }
                
                    // Check if server is up - more precise matching
                    // Look for "server is up" but exclude cases where it says "not up", "down", "stopped", etc.
                    string lowerOutput = statusOutput.ToLowerInvariant();
                    bool hasServerIsUp = lowerOutput.Contains("server is up");
                    bool hasNegativeIndicators = lowerOutput.Contains("server is down") ||
                                               lowerOutput.Contains("server is not up") ||
                                               lowerOutput.Contains("not running") ||
                                               lowerOutput.Contains("stopped") ||
                                               lowerOutput.Contains("offline") ||
                                               lowerOutput.Contains("does not exist") ||
                                               lowerOutput.Contains("not currently running");
                    
                    // Server is up only if we have "server is up" AND no negative indicators
                    bool serverIsUp = hasServerIsUp && !hasNegativeIndicators;
                    
                    System.Diagnostics.Debug.WriteLine($"[{serverName}] hasServerIsUp={hasServerIsUp}, hasNegativeIndicators={hasNegativeIndicators}, serverIsUp={serverIsUp}");
                    
                    if (!serverIsUp)
                    {
                    stats.Status = ServerStatus.Offline;
                    stats.CpuUsage = 0;
                    stats.MemoryUsage = string.Empty;
                    stats.OnlinePlayers = 0;
                    stats.MaxPlayers = 0;
                    stats.GameDay = 0;
                    stats.ServerVersion = string.Empty;
                    stats.ServerPing = string.Empty;
                    System.Diagnostics.Debug.WriteLine($"[{serverName}] ✗ OFFLINE");
                    OnStatusOutputReceived($"[{serverName}] Result: OFFLINE");
                    OnServerStatsUpdated(serverName, stats);
                    return; // Return from async lambda instead of continue
                    }
                    
                    // 2. Server is up - parse additional info from status output
                    stats.Status = ServerStatus.Online;
                    
                    // Parse Players: X/Y format - try multiple patterns
                    System.Diagnostics.Debug.WriteLine($"[{serverName}] ========== FULL STATUS OUTPUT START ==========");
                    System.Diagnostics.Debug.WriteLine($"[{serverName}] {statusOutput}");
                    System.Diagnostics.Debug.WriteLine($"[{serverName}] ========== FULL STATUS OUTPUT END ==========");
                    
                    // Try multiple patterns for players - the format might vary
                    System.Text.RegularExpressions.Match? playersMatch = null;
                    
                    // Pattern 1: "Players: X / Y" or "Players X / Y" (with spaces around /)
                    playersMatch = System.Text.RegularExpressions.Regex.Match(statusOutput, @"[Pp]layers[:\s]+\s*(\d+)\s*/\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    // Pattern 2: "Players: X/Y" or "Players X/Y" (without spaces around /)
                    if (!playersMatch.Success)
                    {
                        playersMatch = System.Text.RegularExpressions.Regex.Match(statusOutput, @"[Pp]layers[:\s]+(\d+)/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }
                    
                    // Pattern 3: "Online: X / Y" or "Online X / Y" (with spaces around /)
                    if (!playersMatch.Success)
                    {
                        playersMatch = System.Text.RegularExpressions.Regex.Match(statusOutput, @"[Oo]nline[:\s]+\s*(\d+)\s*/\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }
                    
                    // Pattern 4: "Online: X/Y" or "Online X/Y" (without spaces around /)
                    if (!playersMatch.Success)
                    {
                        playersMatch = System.Text.RegularExpressions.Regex.Match(statusOutput, @"[Oo]nline[:\s]+(\d+)/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }
                    
                    // Pattern 5: "Játékosok: X / Y" (Hungarian, with spaces)
                    if (!playersMatch.Success)
                    {
                        playersMatch = System.Text.RegularExpressions.Regex.Match(statusOutput, @"[Jj]átékosok[:\s]+\s*(\d+)\s*/\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }
                    
                    // Pattern 6: Find ALL X / Y or X/Y patterns and pick the most likely one (player count)
                    if (!playersMatch.Success)
                    {
                        // Try with spaces first: "X / Y"
                        var allMatches = System.Text.RegularExpressions.Regex.Matches(statusOutput, @"(\d{1,3})\s*/\s*(\d{1,3})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        System.Diagnostics.Debug.WriteLine($"[{serverName}] Found {allMatches.Count} X/Y patterns in output");
                        
                        foreach (System.Text.RegularExpressions.Match match in allMatches)
                        {
                            if (match.Groups.Count >= 3)
                            {
                                if (int.TryParse(match.Groups[1].Value, out int online) && 
                                    int.TryParse(match.Groups[2].Value, out int max))
                                {
                                    System.Diagnostics.Debug.WriteLine($"[{serverName}] Checking pattern: {online}/{max}");
                                    
                                    // Validate: first number should be <= second, and second should be reasonable (10-200)
                                    // This is likely a player count
                                    if (online <= max && max >= 10 && max <= 200)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[{serverName}] ✓ Valid player count pattern found: {online}/{max}");
                                        playersMatch = match;
                                        break; // Use the first valid match
                                    }
                                }
                            }
                        }
                    }
                    
                    if (playersMatch != null && playersMatch.Success && playersMatch.Groups.Count >= 3)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{serverName}] ✓ Players match found: Group1='{playersMatch.Groups[1].Value}', Group2='{playersMatch.Groups[2].Value}'");
                        if (int.TryParse(playersMatch.Groups[1].Value, out int onlinePlayers))
                        {
                            stats.OnlinePlayers = onlinePlayers;
                        }
                        if (int.TryParse(playersMatch.Groups[2].Value, out int maxPlayers))
                        {
                            stats.MaxPlayers = maxPlayers;
                        }
                        System.Diagnostics.Debug.WriteLine($"[{serverName}] ✓ Parsed players: {stats.OnlinePlayers}/{stats.MaxPlayers}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[{serverName}] ✗ WARNING: Could not parse players from status output");
                        System.Diagnostics.Debug.WriteLine($"[{serverName}] Attempted patterns: Players, Online, Játékosok, generic X/Y");
                        stats.OnlinePlayers = 0;
                        stats.MaxPlayers = 0;
                    }
                    
                    // Parse Day: X format
                    var dayMatch = System.Text.RegularExpressions.Regex.Match(statusOutput, @"Day:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (dayMatch.Success && dayMatch.Groups.Count >= 2)
                    {
                        if (int.TryParse(dayMatch.Groups[1].Value, out int gameDay))
                        {
                            stats.GameDay = gameDay;
                        }
                    }
                    
                    // Parse Server Version: X.XX format
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(statusOutput, @"Server Version:\s*([\d.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (versionMatch.Success && versionMatch.Groups.Count >= 2)
                    {
                        stats.ServerVersion = versionMatch.Groups[1].Value.Trim();
                    }
                    
                    // Parse Server Ping: X ms format
                    var pingMatch = System.Text.RegularExpressions.Regex.Match(statusOutput, @"Server Ping:\s*(\d+)\s*ms", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (pingMatch.Success && pingMatch.Groups.Count >= 2)
                    {
                        stats.ServerPing = pingMatch.Groups[1].Value.Trim() + " ms";
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[{serverName}] ✓ ONLINE - Players: {stats.OnlinePlayers}/{stats.MaxPlayers}, Day: {stats.GameDay}, Version: {stats.ServerVersion}, Ping: {stats.ServerPing}");
                    OnStatusOutputReceived($"[{serverName}] Result: ONLINE - Players: {stats.OnlinePlayers}/{stats.MaxPlayers}, Day: {stats.GameDay}, Version: {stats.ServerVersion}, Ping: {stats.ServerPing}");
                    
                    OnServerStatsUpdated(serverName, stats);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{serverName}] Status check error: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    OnStatusOutputReceived($"[{serverName}] ERROR: {ex.Message}");
                    stats.Status = ServerStatus.Offline;
                    stats.CpuUsage = 0;
                    stats.MemoryUsage = string.Empty;
                    stats.OnlinePlayers = 0;
                    stats.MaxPlayers = 0;
                    stats.GameDay = 0;
                    stats.ServerVersion = string.Empty;
                    stats.ServerPing = string.Empty;
                    OnServerStatsUpdated(serverName, stats);
                }
            });

            // Wait for all server status checks to complete in parallel
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Status frissítési hiba: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            OnStatusOutputReceived($"ERROR: Status update failed - {ex.Message}");
        }
    }

    private async Task<string> GetMapNameFromYamlAsync(string directoryPath, string instanceName)
    {
        try
        {
            // Find docker-compose file in Instance_* directory
            string dockerComposePath = string.Empty;
            string findComposeCommand = $"find \"{directoryPath}/Instance_{instanceName}\" -maxdepth 1 -name 'docker-compose-*.yaml' -type f 2>/dev/null | head -1";
            string foundCompose = await _sshService.ExecuteCommandAsync(findComposeCommand);
            
            if (!string.IsNullOrEmpty(foundCompose.Trim()))
            {
                dockerComposePath = foundCompose.Trim();
            }
            else
            {
                // Fallback: try to find any Instance_* directory and docker-compose file
                string findInstanceDirCommand = $"ls -d \"{directoryPath}\"/Instance_* 2>/dev/null | head -1";
                string instanceDir = await _sshService.ExecuteCommandAsync(findInstanceDirCommand);
                if (!string.IsNullOrEmpty(instanceDir.Trim()))
                {
                    string instanceDirPath = instanceDir.Trim();
                    string findInInstanceCommand = $"find \"{instanceDirPath}\" -maxdepth 1 -name 'docker-compose-*.yaml' -type f 2>/dev/null | head -1";
                    string foundInInstance = await _sshService.ExecuteCommandAsync(findInInstanceCommand);
                    if (!string.IsNullOrEmpty(foundInInstance.Trim()))
                    {
                        dockerComposePath = foundInInstance.Trim();
                    }
                }
                
                // Last fallback: try the expected path
                if (string.IsNullOrEmpty(dockerComposePath))
                {
                    dockerComposePath = $"{directoryPath}/Instance_{instanceName}/docker-compose-{instanceName}.yaml";
                }
            }
            
            // Check if file exists
            string checkFileCommand = $"test -f \"{dockerComposePath}\" && echo \"exists\" || echo \"notfound\"";
            string fileCheck = await _sshService.ExecuteCommandAsync(checkFileCommand);
            
            if (!fileCheck.Trim().Contains("exists"))
            {
                System.Diagnostics.Debug.WriteLine($"Docker-compose file not found at: {dockerComposePath}");
                return string.Empty;
            }
            
            // Read the entire docker-compose file and search for MAP_NAME
            string readComposeCommand = $"cat \"{dockerComposePath}\" 2>/dev/null";
            string composeFileContent = await _sshService.ExecuteCommandAsync(readComposeCommand);
            
            if (string.IsNullOrEmpty(composeFileContent))
            {
                return string.Empty;
            }
            
            // Parse MAP_NAME=value (can be with or without indentation/spaces)
            // Try multiple patterns: MAP_NAME=value, MAP_NAME = value, - MAP_NAME=value (YAML list item)
            var mapNameMatch = Regex.Match(composeFileContent, @"MAP_NAME\s*=\s*([^\s\n\r]+)", RegexOptions.IgnoreCase);
            if (mapNameMatch.Success && mapNameMatch.Groups.Count > 1)
            {
                string mapName = mapNameMatch.Groups[1].Value.Trim();
                // Remove quotes if present
                if ((mapName.StartsWith('"') && mapName.EndsWith('"')) || (mapName.StartsWith('\'') && mapName.EndsWith('\'')))
                {
                    mapName = mapName.Substring(1, mapName.Length - 2);
                }
                System.Diagnostics.Debug.WriteLine($"✓ Found MAP_NAME='{mapName}' in docker-compose file: {dockerComposePath}");
                return mapName;
            }
            
            System.Diagnostics.Debug.WriteLine($"✗ MAP_NAME not found in docker-compose file: {dockerComposePath}");
            System.Diagnostics.Debug.WriteLine($"File content preview (first 500 chars): {composeFileContent.Substring(0, Math.Min(500, composeFileContent.Length))}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading MAP_NAME from yaml for instance '{instanceName}': {ex.Message}");
            return string.Empty;
        }
    }

    private async Task<string> GetOrCacheInstanceName(string serverName)
    {
        if (_cachedInstanceNames.TryGetValue(serverName, out var cachedName)) return cachedName;

        if (_serverDirectoryPaths.TryGetValue(serverName, out var path))
        {
            string name = await GetInstanceNameForServerAsync(serverName, path);
            _cachedInstanceNames[serverName] = name;
            return name;
        }
        return serverName;
    }

    private async Task UpdateServerStatsAsync()
    {
        try
        {
            if (_serverStats.Count == 0) return;

            // Get CPU cores count to calculate percentage of total system CPU
            string coresCommand = "nproc";
            string coresOutput = await _sshService.ExecuteCommandAsync(coresCommand);
            int cpuCores = 1;
            if (int.TryParse(coresOutput.Trim(), out int parsedCores))
            {
                cpuCores = parsedCores;
            }

            // A docker stats a legmegbízhatóbb forrás a konténer szintű erőforrásra
            string command = "docker stats --no-stream --format \"{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}\" 2>/dev/null";
            string output = await _sshService.ExecuteCommandAsync(command);
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var serverName in _serverStats.Keys.ToList())
            {
                var stats = _serverStats[serverName];
                if (stats.Status != ServerStatus.Online) 
                {
                    stats.CpuUsage = 0;
                    stats.MemoryUsage = string.Empty;
                    OnServerStatsUpdated(serverName, stats);
                    continue;
                }

                string containerName = $"asa_{await GetOrCacheInstanceName(serverName)}";
                var line = lines.FirstOrDefault(l => l.StartsWith(containerName + "|"));

                if (line != null)
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 3)
                    {
                        // Parse CPU percentage
                        string cpuPercent = parts[1].Trim().TrimEnd('%').Trim();
                        double cpuUsage = 0;
                        if (!string.IsNullOrEmpty(cpuPercent))
                        {
                            string cleanCpu = Regex.Replace(cpuPercent, @"[^\d.]", "");
                            if (double.TryParse(cleanCpu, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedCpu))
                            {
                                // CPUPerc is percentage per core, so divide by number of cores to get total system percentage
                                cpuUsage = parsedCpu / cpuCores;
                                cpuUsage = Math.Min(100.0, cpuUsage);
                            }
                        }
                        
                        stats.CpuUsage = cpuUsage;
                        stats.MemoryUsage = parts[2].Trim();
                    }
                }
                else
                {
                    stats.CpuUsage = 0;
                    stats.MemoryUsage = string.Empty;
                }
                
                OnServerStatsUpdated(serverName, stats);
            }
        }
        catch (Exception ex) 
        { 
            System.Diagnostics.Debug.WriteLine($"Stats hiba: {ex.Message}"); 
        }
    }

    private async Task<string> GetInstanceNameForServerAsync(string serverName, string directoryPath)
    {
        try
        {
            string cmd = $"find \"{directoryPath}\" -maxdepth 1 -type d -name 'Instance_*' 2>/dev/null | head -1";
            string path = await _sshService.ExecuteCommandAsync(cmd);
            if (!string.IsNullOrEmpty(path.Trim()))
            {
                string dirName = path.Trim().Split('/').Last();
                if (dirName.StartsWith("Instance_"))
                {
                    return dirName.Replace("Instance_", "");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error finding instance name for server '{serverName}': {ex.Message}");
        }
        
        // Fallback: if we couldn't find Instance_* directory, try to extract from server name
        if (serverName.Contains('_'))
        {
            string[] parts = serverName.Split('_');
            if (parts.Length > 1)
            {
                return parts[parts.Length - 1];
            }
        }
        
        return serverName;
    }

    private async Task UpdateSystemStatsAsync()
    {
        try
        {
            // System CPU usage
            string cpuCommand = "top -bn1 | grep 'Cpu(s)' | sed 's/.*, *\\([0-9.]*\\)%* id.*/\\1/' | awk '{print 100 - $1}'";
            string cpuOutput = await _sshService.ExecuteCommandAsync(cpuCommand);
            double systemCpuUsage = 0;
            if (double.TryParse(cpuOutput.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedCpu))
            {
                systemCpuUsage = parsedCpu;
            }

            // System Memory usage
            string memoryCommand = "free | grep Mem | awk '{printf \"%.2f\", ($3/$2) * 100.0}'";
            string memoryOutput = await _sshService.ExecuteCommandAsync(memoryCommand);
            double systemMemoryUsage = 0;
            if (double.TryParse(memoryOutput.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedMemory))
            {
                systemMemoryUsage = parsedMemory;
            }

            // Disk usage
            string diskCommand = "df -h / | tail -1 | awk '{print $5}' | sed 's/%//'";
            string diskOutput = await _sshService.ExecuteCommandAsync(diskCommand);
            double diskUsage = 0;
            if (double.TryParse(diskOutput.Trim(), out double parsedDisk))
            {
                diskUsage = parsedDisk;
            }

            // Network stats
            string networkCommand = "cat /proc/net/dev | grep -E 'eth0|ens|enp' | head -1 | awk '{rx=$2; tx=$10; print rx, tx}'";
            string networkOutput = await _sshService.ExecuteCommandAsync(networkCommand);
            double networkRx = 0;
            double networkTx = 0;
            var networkParts = networkOutput.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (networkParts.Length >= 2)
            {
                // Bytes to Mbps (1 byte = 8 bits, 1 Mbps = 1,000,000 bits)
                if (long.TryParse(networkParts[0], out long rxBytes))
                {
                    networkRx = (rxBytes * 8.0) / 1_000_000.0; // Mbps
                }
                if (long.TryParse(networkParts[1], out long txBytes))
                {
                    networkTx = (txBytes * 8.0) / 1_000_000.0; // Mbps
                }
            }

            // Frissítjük az összes szerver statisztikáját a rendszer szintű adatokkal
            foreach (var serverName in _serverStats.Keys.ToList())
            {
                var stats = _serverStats[serverName];
                stats.DiskUsagePercent = diskUsage;
                stats.NetworkRxMbps = networkRx;
                stats.NetworkTxMbps = networkTx;
                stats.SystemCpuUsage = systemCpuUsage;
                stats.SystemMemoryUsagePercent = systemMemoryUsage;
                
                OnServerStatsUpdated(serverName, stats);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"System stats frissítési hiba: {ex.Message}");
        }
    }

    private async Task UpdatePlayerCountsAsync()
    {
        try
        {
            if (_serverStats.Count == 0)
                return;

            foreach (var serverName in _serverStats.Keys.ToList())
            {
                var stats = _serverStats[serverName];
                
                // Get directory path for this server
                if (!_serverDirectoryPaths.TryGetValue(serverName, out string? directoryPath) || string.IsNullOrEmpty(directoryPath))
                {
                    continue;
                }

                string instanceName = await GetOrCacheInstanceName(serverName);

                // Query online players: POK-manager.sh -custom listplayers -{instance_name}
                try
                {
                    string listPlayersCommand = $"cd \"{directoryPath}\" && ./POK-manager.sh -custom listplayers -{instanceName} 2>&1";
                    string playersOutput = await _sshService.ExecuteCommandAsync(listPlayersCommand);
                    
                    // Count non-empty lines (each line is a player)
                    int onlinePlayers = 0;
                    var lines = playersOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        string trimmedLine = line.Trim();
                        // Skip empty lines and common output lines
                        if (!string.IsNullOrEmpty(trimmedLine) && 
                            !trimmedLine.StartsWith("Listing", StringComparison.OrdinalIgnoreCase) &&
                            !trimmedLine.StartsWith("Players", StringComparison.OrdinalIgnoreCase) &&
                            !trimmedLine.StartsWith("---", StringComparison.OrdinalIgnoreCase) &&
                            !trimmedLine.StartsWith("Error", StringComparison.OrdinalIgnoreCase) &&
                            !trimmedLine.StartsWith("Usage", StringComparison.OrdinalIgnoreCase))
                        {
                            onlinePlayers++;
                        }
                    }
                    
                    stats.OnlinePlayers = onlinePlayers;
                }
                catch (Exception ex)
                {
                    stats.OnlinePlayers = 0;
                    System.Diagnostics.Debug.WriteLine($"Error querying players for {instanceName}: {ex.Message}");
                }

                // Read MAX_PLAYERS from docker-compose yaml file
                try
                {
                    string dockerComposePath = string.Empty;
                    string findComposeCommand = $"find \"{directoryPath}/Instance_{instanceName}\" -maxdepth 1 -name 'docker-compose-*.yaml' -type f 2>/dev/null | head -1";
                    string foundCompose = await _sshService.ExecuteCommandAsync(findComposeCommand);
                    
                    if (!string.IsNullOrEmpty(foundCompose.Trim()))
                    {
                        dockerComposePath = foundCompose.Trim();
                    }
                    else
                    {
                        // Fallback: try to find any Instance_* directory and docker-compose file
                        string findInstanceDirCommand = $"ls -d \"{directoryPath}\"/Instance_* 2>/dev/null | head -1";
                        string instanceDir = await _sshService.ExecuteCommandAsync(findInstanceDirCommand);
                        if (!string.IsNullOrEmpty(instanceDir.Trim()))
                        {
                            string instanceDirPath = instanceDir.Trim();
                            string findInInstanceCommand = $"find \"{instanceDirPath}\" -maxdepth 1 -name 'docker-compose-*.yaml' -type f 2>/dev/null | head -1";
                            string foundInInstance = await _sshService.ExecuteCommandAsync(findInInstanceCommand);
                            if (!string.IsNullOrEmpty(foundInInstance.Trim()))
                            {
                                dockerComposePath = foundInInstance.Trim();
                            }
                        }
                        
                        // Last fallback: try the expected path
                        if (string.IsNullOrEmpty(dockerComposePath))
                        {
                            dockerComposePath = $"{directoryPath}/Instance_{instanceName}/docker-compose-{instanceName}.yaml";
                        }
                    }
                    
                    // Check if file exists
                    string checkFileCommand = $"test -f \"{dockerComposePath}\" && echo \"exists\" || echo \"notfound\"";
                    string fileCheck = await _sshService.ExecuteCommandAsync(checkFileCommand);
                    
                    if (fileCheck.Trim().Contains("exists"))
                    {
                        // Read the entire docker-compose file and search for MAX_PLAYERS
                        string readComposeCommand = $"cat \"{dockerComposePath}\" 2>/dev/null";
                        string composeFileContent = await _sshService.ExecuteCommandAsync(readComposeCommand);
                        
                        // Parse MAX_PLAYERS=30
                        int maxPlayers = 0;
                        if (!string.IsNullOrEmpty(composeFileContent))
                        {
                            var maxPlayersMatch = Regex.Match(composeFileContent, @"MAX_PLAYERS\s*=\s*(\d+)", RegexOptions.IgnoreCase);
                            if (maxPlayersMatch.Success && maxPlayersMatch.Groups.Count > 1)
                            {
                                if (int.TryParse(maxPlayersMatch.Groups[1].Value, out int parsedMax))
                                {
                                    maxPlayers = parsedMax;
                                }
                            }
                        }
                        
                        // Only update if we found a value, otherwise keep the current value
                        if (maxPlayers > 0)
                        {
                            stats.MaxPlayers = maxPlayers;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading MAX_PLAYERS for {instanceName}: {ex.Message}");
                }

                // Notify update
                OnServerStatsUpdated(serverName, stats);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Player count frissítési hiba: {ex.Message}");
        }
    }

    public void RegisterServer(string serverName, string? directoryPath = null)
    {
        _serverStats.TryAdd(serverName, new ServerStats { Status = ServerStatus.Offline });
        if (!string.IsNullOrEmpty(directoryPath)) 
        {
            _serverDirectoryPaths.AddOrUpdate(serverName, directoryPath, (key, oldValue) => directoryPath);
        }
    }

    public void UnregisterServer(string serverName)
    {
        _serverStats.TryRemove(serverName, out _);
        _serverDirectoryPaths.TryRemove(serverName, out _);
        _cachedInstanceNames.TryRemove(serverName, out _);
    }

    public ServerStats? GetServerStats(string serverName)
    {
        _serverStats.TryGetValue(serverName, out var stats);
        return stats;
    }

    protected virtual void OnServerStatsUpdated(string serverName, ServerStats stats)
    {
        var args = new ServerStatsEventArgs
        {
            ServerName = serverName,
            Status = stats.Status,
            CpuUsage = stats.CpuUsage,
            MemoryUsage = stats.MemoryUsage,
            DiskUsagePercent = stats.DiskUsagePercent,
            NetworkRxMbps = stats.NetworkRxMbps,
            NetworkTxMbps = stats.NetworkTxMbps,
            SystemCpuUsage = stats.SystemCpuUsage,
            SystemMemoryUsagePercent = stats.SystemMemoryUsagePercent,
            OnlinePlayers = stats.OnlinePlayers,
            MaxPlayers = stats.MaxPlayers,
            GameDay = stats.GameDay,
            ServerVersion = stats.ServerVersion,
            ServerPing = stats.ServerPing,
        };

        ServerStatsUpdated?.Invoke(this, args);
    }

    protected virtual void OnStatusOutputReceived(string message)
    {
        StatusOutputReceived?.Invoke(this, message);
    }

    public void Dispose()
    {
        Stop();
        _cancellationTokenSource?.Dispose();
    }
}
