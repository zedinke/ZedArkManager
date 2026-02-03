using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using ZedASAManager.Models;

namespace ZedASAManager.Services;

public class MonitoringService : IDisposable
{
    private readonly SshService _sshService;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning = false;
    private readonly ConcurrentDictionary<string, ServerStats> _serverStats = new();
    private readonly ConcurrentDictionary<string, string> _serverDirectoryPaths = new(); // ServerName -> DirectoryPath

    public event EventHandler<ServerStatsEventArgs>? ServerStatsUpdated;

    public class ServerStatsEventArgs : EventArgs
    {
        public string ServerName { get; set; } = string.Empty;
        public ServerStatus Status { get; set; }
        public double CpuUsage { get; set; }
        public string MemoryUsage { get; set; } = string.Empty;
        public double DiskUsagePercent { get; set; }
        public double NetworkRxMbps { get; set; } // Download
        public double NetworkTxMbps { get; set; } // Upload
        public double SystemCpuUsage { get; set; } // Rendszer szintű CPU használat
        public double SystemMemoryUsagePercent { get; set; } // Rendszer szintű RAM használat
        public int OnlinePlayers { get; set; }
        public int MaxPlayers { get; set; }
    }

    public class ServerStats
    {
        public ServerStatus Status { get; set; }
        public double CpuUsage { get; set; }
        public string MemoryUsage { get; set; } = string.Empty;
        public double DiskUsagePercent { get; set; }
        public double NetworkRxMbps { get; set; }
        public double NetworkTxMbps { get; set; }
        public double SystemCpuUsage { get; set; } // Rendszer szintű CPU használat
        public double SystemMemoryUsagePercent { get; set; } // Rendszer szintű RAM használat
        public int OnlinePlayers { get; set; }
        public int MaxPlayers { get; set; }
    }

    public MonitoringService(SshService sshService)
    {
        _sshService = sshService;
    }

    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1)); // Másodpercenkénti frissítés

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
                    // Run updates in parallel for better performance
                    await Task.WhenAll(
                        UpdateServerStatusesAsync(),
                        UpdateServerStatsAsync(),
                        UpdateSystemStatsAsync()
                    );
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monitoring hiba: {ex.Message}");
            }

            if (_timer != null)
            {
                await _timer.WaitForNextTickAsync(cancellationToken);
            }
            else
            {
                break;
            }
        }
    }

    private async Task UpdateServerStatusesAsync()
    {
        try
        {
            // Status is now determined by PID (if container has PID, it's running)
            if (_serverStats.Count == 0)
                return;

            // Get list of running containers with their PIDs
            // Format: docker ps --format "{{.Names}}|{{.ID}}" to get container names and IDs
            // Then use docker inspect to get PID
            string command = "docker ps --format \"{{.Names}}\" 2>/dev/null";
            string output = await _sshService.ExecuteCommandAsync(command);
            
            var runningContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string containerName = line.Trim();
                if (!string.IsNullOrEmpty(containerName))
                {
                    runningContainers.Add(containerName);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"=== Running Containers Check ===");
            System.Diagnostics.Debug.WriteLine($"Found {runningContainers.Count} running containers: {string.Join(", ", runningContainers)}");

            // Update status for all known servers based on container PID/running status
            foreach (var serverName in _serverStats.Keys.ToList())
            {
                var stats = _serverStats[serverName];
                
                // Normalize server name for comparison
                string normalizedServerName = serverName.ToLowerInvariant().Trim();
                bool isOnline = false;
                
                // Try to find matching container in running containers
                foreach (var containerName in runningContainers)
                {
                    if (!containerName.StartsWith("asa_"))
                        continue;
                    
                    // Extract server name from container (e.g., "asa_Rexodon-center" -> "Rexodon-center")
                    string extractedServerName = containerName.Substring(4); // Remove "asa_" prefix
                    string normalizedExtractedName = extractedServerName.ToLowerInvariant().Trim();
                    
                    // Try exact match first
                    if (normalizedExtractedName == normalizedServerName)
                    {
                        isOnline = true;
                        System.Diagnostics.Debug.WriteLine($"Server '{serverName}' is ONLINE (matched container '{containerName}')");
                        break;
                    }
                    
                    // Try partial matches
                    if (normalizedExtractedName.Contains(normalizedServerName) || 
                        normalizedServerName.Contains(normalizedExtractedName))
                    {
                        isOnline = true;
                        System.Diagnostics.Debug.WriteLine($"Server '{serverName}' is ONLINE (partial match with container '{containerName}')");
                        break;
                    }
                    
                    // Try matching by removing common suffixes
                    string serverNameBase = normalizedServerName;
                    string extractedBase = normalizedExtractedName;
                    string[] suffixes = { "-center", "-server", "-asa", "-ark" };
                    foreach (var suffix in suffixes)
                    {
                        if (serverNameBase.EndsWith(suffix))
                            serverNameBase = serverNameBase.Substring(0, serverNameBase.Length - suffix.Length);
                        if (extractedBase.EndsWith(suffix))
                            extractedBase = extractedBase.Substring(0, extractedBase.Length - suffix.Length);
                    }
                    
                    if (serverNameBase == extractedBase || 
                        serverNameBase.Contains(extractedBase) || 
                        extractedBase.Contains(serverNameBase))
                    {
                        isOnline = true;
                        System.Diagnostics.Debug.WriteLine($"Server '{serverName}' is ONLINE (suffix match with container '{containerName}')");
                        break;
                    }
                }
                
                // If no match found, check if we can get PID directly for the container
                if (!isOnline)
                {
                    // Try to get PID for container asa_{serverName}
                    string containerNameToCheck = $"asa_{serverName}";
                    string pidCommand = $"docker inspect --format '{{{{.State.Pid}}}}' {containerNameToCheck} 2>/dev/null";
                    string pidOutput = await _sshService.ExecuteCommandAsync(pidCommand);
                    
                    if (!string.IsNullOrWhiteSpace(pidOutput) && int.TryParse(pidOutput.Trim(), out int pid) && pid > 0)
                    {
                        isOnline = true;
                        System.Diagnostics.Debug.WriteLine($"Server '{serverName}' is ONLINE (PID={pid})");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Server '{serverName}' is OFFLINE (no PID found)");
                    }
                }
                
                stats.Status = isOnline ? ServerStatus.Online : ServerStatus.Offline;
                
                // Always notify to ensure UI is updated
                OnServerStatsUpdated(serverName, stats);
            }
            
            System.Diagnostics.Debug.WriteLine($"=== End Server Status Check ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Status frissítési hiba: {ex.Message}");
        }
    }

    private double ParseMemoryUsageToGb(string memoryUsage)
    {
        // Parse string like "9.14GiB / 20GiB" to get used RAM in GB
        try
        {
            if (string.IsNullOrEmpty(memoryUsage))
                return 0;

            // Split by "/" to get used and total
            var parts = memoryUsage.Split('/');
            if (parts.Length < 1)
                return 0;

            string usedPart = parts[0].Trim();
            
            // Extract number and unit (e.g., "9.14GiB" -> 9.14, "GiB")
            var match = Regex.Match(usedPart, @"([\d.]+)\s*(GiB|MiB|KiB|GB|MB|KB|B)", RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count >= 3)
            {
                if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double value))
                {
                    string unit = match.Groups[2].Value.ToUpperInvariant();
                    
                    // Convert to GB
                    return unit switch
                    {
                        "GB" or "GIB" => value,
                        "MB" or "MIB" => value / 1024.0,
                        "KB" or "KIB" => value / (1024.0 * 1024.0),
                        "B" => value / (1024.0 * 1024.0 * 1024.0),
                        _ => value // Default to GB if unknown
                    };
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RAM parse hiba: {ex.Message}");
        }
        
        return 0;
    }

    private async Task UpdateServerStatsAsync()
    {
        try
        {
            if (_serverStats.Count == 0)
                return;
            
            // Get CPU cores count to calculate percentage of total system CPU
            string coresCommand = "nproc";
            string coresOutput = await _sshService.ExecuteCommandAsync(coresCommand);
            int cpuCores = 1;
            if (int.TryParse(coresOutput.Trim(), out int parsedCores))
            {
                cpuCores = parsedCores;
            }
                
            // Get stats for all containers, then filter for our asa_ containers
            // CPUPerc shows CPU usage - if it's 200% on a 4-core system, that means 2 cores fully used
            // To show as percentage of total system: (200% / 4 cores) * 100 = 50%
            // We want to show: out of all physical cores, what percentage does this server use
            string command = "docker stats --no-stream --format \"{{.Name}}|{{.MemUsage}}|{{.CPUPerc}}\" 2>/dev/null";
            string output = await _sshService.ExecuteCommandAsync(command);

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Create a dictionary to map container names to their stats
            var containerStats = new Dictionary<string, (string memoryUsage, double cpuUsage)>();
            
            System.Diagnostics.Debug.WriteLine($"=== Docker Stats Output ===");
            System.Diagnostics.Debug.WriteLine($"Raw output: {output}");
            System.Diagnostics.Debug.WriteLine($"Lines count: {lines.Length}");
            
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    string containerName = parts[0].Trim();
                    string memoryUsage = parts[1].Trim();
                    string cpuPercent = parts[2].Trim().TrimEnd('%').Trim();

                    // Only process containers that start with "asa_"
                    if (!containerName.StartsWith("asa_"))
                        continue;

                    System.Diagnostics.Debug.WriteLine($"Processing container: {containerName}, Memory: {memoryUsage}, CPU: {cpuPercent}");

                    // Parse CPU percentage
                    double cpuUsage = 0;
                    if (!string.IsNullOrEmpty(cpuPercent))
                    {
                        // Remove any non-numeric characters except decimal point
                        string cleanCpu = Regex.Replace(cpuPercent, @"[^\d.]", "");
                        if (double.TryParse(cleanCpu, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedCpu))
                        {
                            // CPUPerc is percentage per core, so divide by number of cores to get total system percentage
                            // Example: 8% on 4 cores = 8/4 = 2% of total system
                            cpuUsage = parsedCpu / cpuCores;
                            
                            // Cap at 100% since that's the maximum of total system
                            cpuUsage = Math.Min(100.0, cpuUsage);
                        }
                    }
                    
                    // Store container stats
                    containerStats[containerName] = (memoryUsage, cpuUsage);
                    System.Diagnostics.Debug.WriteLine($"Stored stats for container '{containerName}': Memory={memoryUsage}, CPU={cpuUsage:F2}%");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"Total asa_ containers found: {containerStats.Count}");
            
            // Now update all tracked servers, matching containers to servers
            foreach (var serverName in _serverStats.Keys.ToList())
            {
                var stats = _serverStats[serverName];
                bool foundMatch = false;
                
                // Normalize server name for comparison
                string normalizedServerName = serverName.ToLowerInvariant().Trim();
                
                // Try to find matching container
                foreach (var containerName in containerStats.Keys)
                {
                    if (!containerName.StartsWith("asa_"))
                        continue;
                    
                    // Extract server name from container (e.g., "asa_Rexodon-center" -> "Rexodon-center")
                    string extractedServerName = containerName.Substring(4); // Remove "asa_" prefix
                    string normalizedExtractedName = extractedServerName.ToLowerInvariant().Trim();
                    
                    // Try exact match first
                    if (normalizedExtractedName == normalizedServerName)
                    {
                        var (memoryUsage, cpuUsage) = containerStats[containerName];
                        stats.CpuUsage = cpuUsage;
                        stats.MemoryUsage = memoryUsage;
                        foundMatch = true;
                        System.Diagnostics.Debug.WriteLine($"Matched server '{serverName}' to container '{containerName}': CPU={cpuUsage:F2}%, Memory={memoryUsage}");
                        break;
                    }
                    
                    // Try partial matches
                    if (normalizedExtractedName.Contains(normalizedServerName) || 
                        normalizedServerName.Contains(normalizedExtractedName))
                    {
                        var (memoryUsage, cpuUsage) = containerStats[containerName];
                        stats.CpuUsage = cpuUsage;
                        stats.MemoryUsage = memoryUsage;
                        foundMatch = true;
                        System.Diagnostics.Debug.WriteLine($"Matched server '{serverName}' to container '{containerName}' (partial): CPU={cpuUsage:F2}%, Memory={memoryUsage}");
                        break;
                    }
                    
                    // Try matching by removing common suffixes
                    string serverNameBase = normalizedServerName;
                    string extractedBase = normalizedExtractedName;
                    string[] suffixes = { "-center", "-server", "-asa", "-ark" };
                    foreach (var suffix in suffixes)
                    {
                        if (serverNameBase.EndsWith(suffix))
                            serverNameBase = serverNameBase.Substring(0, serverNameBase.Length - suffix.Length);
                        if (extractedBase.EndsWith(suffix))
                            extractedBase = extractedBase.Substring(0, extractedBase.Length - suffix.Length);
                    }
                    
                    if (serverNameBase == extractedBase || 
                        serverNameBase.Contains(extractedBase) || 
                        extractedBase.Contains(serverNameBase))
                    {
                        var (memoryUsage, cpuUsage) = containerStats[containerName];
                        stats.CpuUsage = cpuUsage;
                        stats.MemoryUsage = memoryUsage;
                        foundMatch = true;
                        System.Diagnostics.Debug.WriteLine($"Matched server '{serverName}' to container '{containerName}' (suffix removed): CPU={cpuUsage:F2}%, Memory={memoryUsage}");
                        break;
                    }
                }
                
                // If no match found, reset stats to 0/empty
                if (!foundMatch)
                {
                    stats.CpuUsage = 0;
                    stats.MemoryUsage = string.Empty;
                    System.Diagnostics.Debug.WriteLine($"No container match for server '{serverName}', resetting stats. Available containers: {string.Join(", ", containerStats.Keys)}");
                }
                
                // Always notify to ensure UI is updated
                OnServerStatsUpdated(serverName, stats);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Stats frissítési hiba: {ex.Message}");
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
    }

    public ServerStats? GetServerStats(string serverName)
    {
        _serverStats.TryGetValue(serverName, out var stats);
        return stats;
    }

    private async Task UpdateSystemStatsAsync()
    {
        try
        {
            // System CPU usage: top -bn1 | grep "Cpu(s)" | awk '{print $2}' | cut -d'%' -f1
            // Vagy: vmstat 1 2 | tail -1 | awk '{print 100 - $15}'
            // Egyszerűbb: top -bn1 | grep "Cpu(s)" | sed "s/.*, *\([0-9.]*\)%* id.*/\1/" | awk '{print 100 - $1}'
            string cpuCommand = "top -bn1 | grep 'Cpu(s)' | sed 's/.*, *\\([0-9.]*\\)%* id.*/\\1/' | awk '{print 100 - $1}'";
            string cpuOutput = await _sshService.ExecuteCommandAsync(cpuCommand);
            
            double systemCpuUsage = 0;
            if (double.TryParse(cpuOutput.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedCpu))
            {
                systemCpuUsage = parsedCpu;
            }
            
            // System Memory usage: free | grep Mem | awk '{printf "%.2f", ($3/$2) * 100.0}'
            string memoryCommand = "free | grep Mem | awk '{printf \"%.2f\", ($3/$2) * 100.0}'";
            string memoryOutput = await _sshService.ExecuteCommandAsync(memoryCommand);
            
            double systemMemoryUsage = 0;
            if (double.TryParse(memoryOutput.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedMemory))
            {
                systemMemoryUsage = parsedMemory;
            }
            
            // Disk usage: df -h / | tail -1 | awk '{print $5}' | sed 's/%//'
            string diskCommand = "df -h / | tail -1 | awk '{print $5}' | sed 's/%//'";
            string diskOutput = await _sshService.ExecuteCommandAsync(diskCommand);
            
            double diskUsage = 0;
            if (double.TryParse(diskOutput.Trim(), out double parsedDisk))
            {
                diskUsage = parsedDisk;
            }

            // Network stats: cat /proc/net/dev | grep -E 'eth0|ens|enp' | awk '{rx+=$2; tx+=$10} END {print rx, tx}'
            // Vagy használhatjuk az iftop-ot vagy más eszközt
            // Egyszerűsített verzió: docker stats --no-stream --format "{{.NetIO}}"
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
            OnlinePlayers = stats.OnlinePlayers,
            MaxPlayers = stats.MaxPlayers
        };

        ServerStatsUpdated?.Invoke(this, args);
    }

    private double ParseNetworkSize(string size)
    {
        // Parse size like "1.2GB", "500MB", "0B", etc. and convert to Mbps
        size = size.Trim();
        if (string.IsNullOrEmpty(size) || size == "0B")
            return 0;

        double multiplier = 1;
        if (size.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 8 * 1000; // GB to Mbps (1 GB = 1000 MB, 1 MB = 8 Mbps)
            size = size.Substring(0, size.Length - 2).Trim();
        }
        else if (size.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 8; // MB to Mbps
            size = size.Substring(0, size.Length - 2).Trim();
        }
        else if (size.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 8.0 / 1000.0; // KB to Mbps
            size = size.Substring(0, size.Length - 2).Trim();
        }
        else if (size.EndsWith("B", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 8.0 / 1_000_000.0; // Bytes to Mbps
            size = size.Substring(0, size.Length - 1).Trim();
        }

        if (double.TryParse(size, out double value))
        {
            return value * multiplier;
        }

        return 0;
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
                    // If no directory path, skip this server
                    continue;
                }

                // Extract instance name from Instance_* directories
                // The structure is: /home/user/asa_server/Cluster_Name_servermappaneve/Instance_servermappaneve
                // We need to find the Instance_* directory and extract the name after Instance_
                string instanceName = string.Empty;
                
                try
                {
                    // Find Instance_* directories in the server directory
                    string findInstanceCommand = $"find \"{directoryPath}\" -maxdepth 1 -type d -name 'Instance_*' 2>/dev/null | head -1";
                    string foundInstancePath = await _sshService.ExecuteCommandAsync(findInstanceCommand);
                    
                    if (!string.IsNullOrEmpty(foundInstancePath.Trim()))
                    {
                        // Extract instance name from path like: /path/to/Instance_aberrationteszt
                        string instancePath = foundInstancePath.Trim();
                        int lastSlash = instancePath.LastIndexOf('/');
                        if (lastSlash >= 0 && lastSlash < instancePath.Length - 1)
                        {
                            string instanceDirName = instancePath.Substring(lastSlash + 1);
                            if (instanceDirName.StartsWith("Instance_"))
                            {
                                instanceName = instanceDirName.Substring("Instance_".Length);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error finding instance directory: {ex.Message}");
                }

                // Fallback: if we couldn't find Instance_* directory, try to extract from server name
                if (string.IsNullOrEmpty(instanceName))
                {
                    if (serverName.Contains('_'))
                    {
                        string[] parts = serverName.Split('_');
                        if (parts.Length > 1)
                        {
                            // Take the last part as instance name
                            instanceName = parts[parts.Length - 1];
                        }
                    }
                    else
                    {
                        instanceName = serverName;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"UpdatePlayerCounts: serverName={serverName}, directoryPath={directoryPath}, instanceName={instanceName}");

                // Query online players: POK-manager.sh -custom listplayers -{instance_name}
                try
                {
                    string listPlayersCommand = $"cd \"{directoryPath}\" && ./POK-manager.sh -custom listplayers -{instanceName} 2>&1";
                    string playersOutput = await _sshService.ExecuteCommandAsync(listPlayersCommand);
                    
                    System.Diagnostics.Debug.WriteLine($"ListPlayers output for {instanceName}: {playersOutput}");
                    
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
                    System.Diagnostics.Debug.WriteLine($"Online players for {instanceName}: {onlinePlayers}");
                }
                catch (Exception ex)
                {
                    // If command fails, set to 0
                    stats.OnlinePlayers = 0;
                    System.Diagnostics.Debug.WriteLine($"Error querying players for {instanceName}: {ex.Message}");
                }

                // Read MAX_PLAYERS from docker-compose yaml file
                try
                {
                    // Find docker-compose file in Instance_* directory
                    // Path format: /home/user/asa_server/Cluster_Name_servermappaneve/Instance_servermappaneve/docker-compose-servermappaneve.yaml
                    string dockerComposePath = string.Empty;
                    
                    // First try to find any docker-compose file in Instance_* directories
                    // Simpler approach: find Instance_* directories, then find docker-compose files in them
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
                    
                    System.Diagnostics.Debug.WriteLine($"Docker-compose path for {instanceName}: {dockerComposePath}");
                    
                    // Check if file exists
                    string checkFileCommand = $"test -f \"{dockerComposePath}\" && echo \"exists\" || echo \"notfound\"";
                    string fileCheck = await _sshService.ExecuteCommandAsync(checkFileCommand);
                    System.Diagnostics.Debug.WriteLine($"File check result: {fileCheck.Trim()}");
                    
                    if (fileCheck.Trim().Contains("exists"))
                    {
                        // Read the entire docker-compose file and search for MAX_PLAYERS
                        // The format in YAML is: "      - MAX_PLAYERS=30" (with indentation)
                        string readComposeCommand = $"cat \"{dockerComposePath}\" 2>/dev/null";
                        string composeFileContent = await _sshService.ExecuteCommandAsync(readComposeCommand);
                        
                        System.Diagnostics.Debug.WriteLine($"Docker-compose file content length for {instanceName}: {composeFileContent?.Length ?? 0}");
                        
                        // Parse MAX_PLAYERS=30 (can be with or without indentation/spaces)
                        int maxPlayers = 0;
                        if (!string.IsNullOrEmpty(composeFileContent))
                        {
                            // Search for MAX_PLAYERS= followed by digits in the entire file
                            var maxPlayersMatch = Regex.Match(composeFileContent, @"MAX_PLAYERS\s*=\s*(\d+)", RegexOptions.IgnoreCase);
                            if (maxPlayersMatch.Success && maxPlayersMatch.Groups.Count > 1)
                            {
                                if (int.TryParse(maxPlayersMatch.Groups[1].Value, out int parsedMax))
                                {
                                    maxPlayers = parsedMax;
                                }
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Max players for {instanceName}: {maxPlayers}");
                        
                        // Only update if we found a value, otherwise keep the current value
                        if (maxPlayers > 0)
                        {
                            stats.MaxPlayers = maxPlayers;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Docker-compose file not found at: {dockerComposePath}");
                    }
                }
                catch (Exception ex)
                {
                    // If file read fails, keep current value
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

    public void Dispose()
    {
        Stop();
        _cancellationTokenSource?.Dispose();
    }
}
