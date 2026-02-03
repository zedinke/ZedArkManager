using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public partial class AddServerWindow : Window
{
    private readonly SshService _sshService;
    private readonly string _username;
    private readonly ObservableCollection<ClusterInfo> _clusters = new();
    private int _asaPort;
    private int _rconPort;

    public AddServerWindow(SshService sshService, string username)
    {
        InitializeComponent();
        _sshService = sshService;
        _username = username;
        ClusterComboBox.ItemsSource = _clusters;
        ClusterComboBox.SelectionChanged += ClusterComboBox_SelectionChanged;
        LoadLocalizedStrings();
        _ = LoadClustersAndPortsAsync();
    }

    private async void ClusterComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ClusterComboBox.SelectedItem is ClusterInfo selectedCluster)
        {
            try
            {
                // Read cluster ID from cluster_id.txt file
                string clusterIdFilePath = $"{selectedCluster.FullPath}/cluster_id.txt";
                string clusterId = await _sshService.ReadFileAsync(clusterIdFilePath);
                clusterId = clusterId.Trim();
                
                if (!string.IsNullOrEmpty(clusterId))
                {
                    ClusterIdTextBox.Text = clusterId;
                }
            }
            catch
            {
                // If file doesn't exist or can't be read, leave empty
                ClusterIdTextBox.Text = string.Empty;
            }
        }
    }

    private void LoadLocalizedStrings()
    {
        Title = LocalizationHelper.GetString("add_server");
        TitleTextBlock.Text = LocalizationHelper.GetString("add_server");
        ClusterLabel.Text = LocalizationHelper.GetString("select_cluster") + ":";
        ServerFolderNameLabel.Text = LocalizationHelper.GetString("server_folder_name") + ":";
        AsaPortLabel.Text = LocalizationHelper.GetString("asa_port") + ":";
        RconPortLabel.Text = LocalizationHelper.GetString("rcon_port") + ":";
        BattleEyeCheckBox.Content = LocalizationHelper.GetString("battleeye");
        ApiCheckBox.Content = LocalizationHelper.GetString("api");
        RconEnabledCheckBox.Content = LocalizationHelper.GetString("rcon_enabled");
        DisplayPokMonitorMessageCheckBox.Content = LocalizationHelper.GetString("display_pok_monitor_message");
        RandomStartupDelayCheckBox.Content = LocalizationHelper.GetString("random_startup_delay");
        CpuOptimizationCheckBox.Content = LocalizationHelper.GetString("cpu_optimization");
        UpdateServerCheckBox.Content = LocalizationHelper.GetString("update_server");
        EnableMotdCheckBox.Content = LocalizationHelper.GetString("enable_motd");
        MotdLabel.Text = LocalizationHelper.GetString("motd") + ":";
        MotdDurationLabel.Text = LocalizationHelper.GetString("motd_duration") + ":";
        MapNameLabel.Text = LocalizationHelper.GetString("map_name") + ":";
        SessionNameLabel.Text = LocalizationHelper.GetString("session_name") + ":";
        ServerAdminPasswordLabel.Text = LocalizationHelper.GetString("server_admin_password") + ":";
        ServerPasswordLabel.Text = LocalizationHelper.GetString("server_password") + ":";
        MaxPlayersLabel.Text = LocalizationHelper.GetString("max_players") + ":";
        ShowAdminCommandsInChatCheckBox.Content = LocalizationHelper.GetString("show_admin_commands_in_chat");
        ClusterIdLabel.Text = LocalizationHelper.GetString("cluster_id") + ":";
        ModIdsLabel.Text = LocalizationHelper.GetString("mod_ids") + ":";
        PassiveModsLabel.Text = LocalizationHelper.GetString("passive_mods") + ":";
        CustomServerArgsLabel.Text = LocalizationHelper.GetString("custom_server_args") + ":";
        MemLimitLabel.Text = LocalizationHelper.GetString("mem_limit") + ":";
        CreateButton.Content = LocalizationHelper.GetString("create");
        CancelButton.Content = LocalizationHelper.GetString("cancel");
    }

    private async Task LoadClustersAndPortsAsync()
    {
        try
        {
            // Load clusters
            string basePath = $"/home/{_username}/asa_server";
            string clusterCommand = $"ls -d {basePath}/Cluster_* 2>/dev/null | sed 's|{basePath}/||'";
            string clusterOutput = await _sshService.ExecuteCommandAsync(clusterCommand);

            _clusters.Clear();
            var lines = clusterOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var clusterName = line.Trim();
                if (!string.IsNullOrEmpty(clusterName) && clusterName.StartsWith("Cluster_"))
                {
                    _clusters.Add(new ClusterInfo
                    {
                        Name = clusterName,
                        FullPath = $"{basePath}/{clusterName}"
                    });
                }
            }

            // Dictionary to store port -> server name mapping
            var portToServerMap = new Dictionary<int, string>();
            var usedPorts = new HashSet<int>();

            // Load used ports from all .env files
            string envPortCommand = $"for dir in {basePath}/*/; do if [ -f \"$dir/.env\" ]; then server_name=$(basename \"$dir\"); grep -E '^(ASA_PORT|RCON_PORT|SERVER_PORT)=' \"$dir/.env\" 2>/dev/null | sed \"s|^|$server_name:|;\"; fi; done";
            string envPortOutput = await _sshService.ExecuteCommandAsync(envPortCommand);

            var envPortLines = envPortOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in envPortLines)
            {
                // Format: servername:ASA_PORT=7783
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                {
                    string serverName = parts[0].Trim();
                    string portLine = parts[1].Trim();
                    var match = Regex.Match(portLine, @"(?:ASA_PORT|RCON_PORT|SERVER_PORT)=(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int port))
                    {
                        usedPorts.Add(port);
                        if (!portToServerMap.ContainsKey(port))
                        {
                            portToServerMap[port] = serverName;
                        }
                    }
                }
            }

            // Load used ports from all yaml files in asa_server directory
            // First, get all yaml files with their directory paths
            string yamlFilesCommand = $"find {basePath} -name '*.yaml' -o -name '*.yml' 2>/dev/null";
            string yamlFilesOutput = await _sshService.ExecuteCommandAsync(yamlFilesCommand);
            var yamlFiles = yamlFilesOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var yamlFile in yamlFiles)
            {
                if (string.IsNullOrWhiteSpace(yamlFile))
                    continue;

                // Extract server name from path: /home/user/asa_server/Cluster_Name_servermappaneve/Instance_servermappaneve/docker-compose-servermappaneve.yaml
                // or: /home/user/asa_server/Cluster_Name_servermappaneve/docker-compose-servermappaneve.yaml
                string serverName = "Unknown";
                var pathParts = yamlFile.Trim().Split('/');
                if (pathParts.Length > 0)
                {
                    // Get the directory name that contains the yaml file
                    // If it's in Instance_* directory, use that directory's parent
                    for (int i = pathParts.Length - 1; i >= 0; i--)
                    {
                        if (pathParts[i].StartsWith("Instance_"))
                        {
                            // Server name is the parent directory
                            if (i > 0)
                            {
                                serverName = pathParts[i - 1];
                            }
                            break;
                        }
                        else if (pathParts[i].Contains("Cluster_") || (!pathParts[i].EndsWith(".yaml") && !pathParts[i].EndsWith(".yml")))
                        {
                            serverName = pathParts[i];
                            break;
                        }
                    }
                }

                // Read the yaml file content
                string yamlContent = await _sshService.ReadFileAsync(yamlFile.Trim());
                var yamlLines = yamlContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in yamlLines)
                {
                    // Match environment variable format: - ASA_PORT=7783 or -ASA_PORT=7783
                    var envMatch = Regex.Match(line, @"-?\s*ASA_PORT[=:](\d+)", RegexOptions.IgnoreCase);
                    if (envMatch.Success && int.TryParse(envMatch.Groups[1].Value, out int asaPort))
                    {
                        usedPorts.Add(asaPort);
                        if (!portToServerMap.ContainsKey(asaPort))
                        {
                            portToServerMap[asaPort] = serverName;
                        }
                    }

                    var rconMatch = Regex.Match(line, @"-?\s*RCON_PORT[=:](\d+)", RegexOptions.IgnoreCase);
                    if (rconMatch.Success && int.TryParse(rconMatch.Groups[1].Value, out int rconPort))
                    {
                        usedPorts.Add(rconPort);
                        if (!portToServerMap.ContainsKey(rconPort))
                        {
                            portToServerMap[rconPort] = serverName;
                        }
                    }

                    var serverMatch = Regex.Match(line, @"-?\s*SERVER_PORT[=:](\d+)", RegexOptions.IgnoreCase);
                    if (serverMatch.Success && int.TryParse(serverMatch.Groups[1].Value, out int serverPort))
                    {
                        usedPorts.Add(serverPort);
                        if (!portToServerMap.ContainsKey(serverPort))
                        {
                            portToServerMap[serverPort] = serverName;
                        }
                    }

                    // Match ports section format: - "7783:7783/tcp" or - "7783:7783/udp"
                    var portsMatch = Regex.Match(line, @"-?\s*""(\d+):\d+/(?:tcp|udp)""");
                    if (portsMatch.Success && int.TryParse(portsMatch.Groups[1].Value, out int portFromYaml))
                    {
                        usedPorts.Add(portFromYaml);
                        if (!portToServerMap.ContainsKey(portFromYaml))
                        {
                            portToServerMap[portFromYaml] = serverName;
                        }
                    }
                }
            }

            // Build the display text for used ports
            var serverPortGroups = portToServerMap
                .GroupBy(kvp => kvp.Value)
                .OrderBy(g => g.Key)
                .ToList();

            var displayText = new StringBuilder();
            if (serverPortGroups.Any())
            {
                foreach (var group in serverPortGroups)
                {
                    var asaPorts = group.Where(kvp => kvp.Key >= 7000 && kvp.Key < 10000).Select(kvp => kvp.Key).OrderBy(p => p).ToList();
                    var rconPorts = group.Where(kvp => kvp.Key >= 27000 && kvp.Key < 28000).Select(kvp => kvp.Key).OrderBy(p => p).ToList();
                    var otherPorts = group.Where(kvp => !(kvp.Key >= 7000 && kvp.Key < 10000) && !(kvp.Key >= 27000 && kvp.Key < 28000)).Select(kvp => kvp.Key).OrderBy(p => p).ToList();

                    displayText.Append($"{group.Key}: ");
                    var portList = new List<string>();
                    if (asaPorts.Any())
                    {
                        portList.Add($"ASA_PORT={string.Join(",", asaPorts)}");
                    }
                    if (rconPorts.Any())
                    {
                        portList.Add($"RCON_PORT={string.Join(",", rconPorts)}");
                    }
                    if (otherPorts.Any())
                    {
                        portList.Add($"SERVER_PORT={string.Join(",", otherPorts)}");
                    }
                    displayText.Append(string.Join(", ", portList));
                    displayText.AppendLine();
                }
            }
            else
            {
                displayText.AppendLine("Nincs foglalt port.");
            }

            UsedPortsTextBlock.Text = displayText.ToString().TrimEnd();

            // Generate free ports
            _asaPort = FindFreePort(usedPorts, 7783);
            _rconPort = FindFreePort(usedPorts, 27026);

            AsaPortTextBox.Text = _asaPort.ToString();
            RconPortTextBox.Text = _rconPort.ToString();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Hiba a clusterek és portok betöltésekor: {ex.Message}",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private int FindFreePort(HashSet<int> usedPorts, int startPort)
    {
        int port = startPort;
        while (usedPorts.Contains(port))
        {
            port++;
        }
        return port;
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        // Validate inputs
        if (ClusterComboBox.SelectedItem == null)
        {
            MessageBox.Show(
                LocalizationHelper.GetString("select_cluster") + " " + LocalizationHelper.GetString("error"),
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ServerFolderNameTextBox.Text))
        {
            MessageBox.Show(
                LocalizationHelper.GetString("server_folder_name") + " " + LocalizationHelper.GetString("error"),
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ClusterIdTextBox.Text))
        {
            MessageBox.Show(
                LocalizationHelper.GetString("cluster_id") + " " + LocalizationHelper.GetString("error"),
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            CreateButton.IsEnabled = false;
            CreateButton.Content = LocalizationHelper.GetString("loading") + "...";

            var selectedCluster = (ClusterInfo)ClusterComboBox.SelectedItem;
            string serverFolderName = ServerFolderNameTextBox.Text.Trim();
            string serverDirectoryName = $"{selectedCluster.Name}_{serverFolderName}";
            string basePath = $"/home/{_username}/asa_server";
            string serverPath = $"{basePath}/{serverDirectoryName}";

            // Create directory
            string mkdirCommand = $"mkdir -p \"{serverPath}\"";
            await _sshService.ExecuteCommandAsync(mkdirCommand);

            // Clone repository and setup
            string setupCommand = $"cd \"{serverPath}\" && git clone https://github.com/Acekorneya/Ark-Survival-Ascended-Server.git && mv Ark-Survival-Ascended-Server/POK-manager.sh . && chmod +x POK-manager.sh && mv Ark-Survival-Ascended-Server/defaults . && rm -rf Ark-Survival-Ascended-Server";
            await _sshService.ExecuteCommandAsync(setupCommand);

            // Wait 5 seconds
            StatusTextBlock.Text = LocalizationHelper.GetString("loading") + "...";
            StatusTextBlock.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Visible;
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Run POK-manager.sh -setup in noninteractive mode
            StatusTextBlock.Text = LocalizationHelper.GetString("running_setup");
            string setupPokCommand = $"cd \"{serverPath}\" && yes | ./POK-manager.sh -setup 2>&1";
            await _sshService.ExecuteCommandAsync(setupPokCommand);

            // Create Instance_{servermappa_neve} directory
            StatusTextBlock.Text = LocalizationHelper.GetString("creating_instance_directory");
            string instanceDirName = $"Instance_{serverFolderName}";
            string instanceDirPath = $"{serverPath}/{instanceDirName}";
            string mkdirInstanceCommand = $"mkdir -p \"{instanceDirPath}\"";
            await _sshService.ExecuteCommandAsync(mkdirInstanceCommand);

            // Create docker-compose file
            StatusTextBlock.Text = "Creating docker-compose file...";
            string dockerComposeFileName = $"docker-compose-{serverFolderName}.yaml";
            string dockerComposeFilePath = $"{instanceDirPath}/{dockerComposeFileName}";
            
            var dockerComposeContent = new StringBuilder();
            dockerComposeContent.AppendLine("version: '2.4'");
            dockerComposeContent.AppendLine("");
            dockerComposeContent.AppendLine("services:");
            dockerComposeContent.AppendLine("  asaserver:");
            dockerComposeContent.AppendLine("    build: .");
            dockerComposeContent.AppendLine("    image: acekorneya/asa_server:2_1_latest");
            dockerComposeContent.AppendLine($"    container_name: asa_{serverFolderName}");
            dockerComposeContent.AppendLine("    restart: unless-stopped");
            dockerComposeContent.AppendLine("    environment:");
            dockerComposeContent.AppendLine($"      - INSTANCE_NAME={serverFolderName}");
            dockerComposeContent.AppendLine("      - TZ=Europe/Berlin");
            dockerComposeContent.AppendLine($"      - BATTLEEYE={BattleEyeCheckBox.IsChecked.Value.ToString().ToUpper()}");
            dockerComposeContent.AppendLine($"      - API={ApiCheckBox.IsChecked.Value.ToString().ToUpper()}");
            dockerComposeContent.AppendLine($"      - RCON_ENABLED={RconEnabledCheckBox.IsChecked.Value.ToString().ToUpper()}");
            dockerComposeContent.AppendLine($"      - DISPLAY_POK_MONITOR_MESSAGE={DisplayPokMonitorMessageCheckBox.IsChecked.Value.ToString().ToUpper()}");
            dockerComposeContent.AppendLine($"      - RANDOM_STARTUP_DELAY={RandomStartupDelayCheckBox.IsChecked.Value.ToString().ToUpper()}");
            dockerComposeContent.AppendLine($"      - CPU_OPTIMIZATION={CpuOptimizationCheckBox.IsChecked.Value.ToString().ToUpper()}");
            dockerComposeContent.AppendLine($"      - UPDATE_SERVER={UpdateServerCheckBox.IsChecked.Value.ToString().ToUpper()}");
            dockerComposeContent.AppendLine("      - CHECK_FOR_UPDATE_INTERVAL=24");
            dockerComposeContent.AppendLine("      - UPDATE_WINDOW_MINIMUM_TIME=03:00 AM");
            dockerComposeContent.AppendLine("      - UPDATE_WINDOW_MAXIMUM_TIME=05:00 AM");
            dockerComposeContent.AppendLine("      - RESTART_NOTICE_MINUTES=30");
            dockerComposeContent.AppendLine($"      - ENABLE_MOTD={EnableMotdCheckBox.IsChecked.Value.ToString().ToUpper()}");
            dockerComposeContent.AppendLine($"      - MOTD={MotdTextBox.Text}");
            dockerComposeContent.AppendLine($"      - MOTD_DURATION={MotdDurationTextBox.Text}");
            dockerComposeContent.AppendLine($"      - MAP_NAME={MapNameTextBox.Text}");
            dockerComposeContent.AppendLine($"      - SESSION_NAME={SessionNameTextBox.Text}");
            dockerComposeContent.AppendLine($"      - SERVER_ADMIN_PASSWORD={ServerAdminPasswordTextBox.Text}");
            dockerComposeContent.AppendLine($"      - SERVER_PASSWORD={ServerPasswordTextBox.Text}");
            dockerComposeContent.AppendLine($"      - ASA_PORT={_asaPort}");
            dockerComposeContent.AppendLine($"      - RCON_PORT={_rconPort}");
            dockerComposeContent.AppendLine($"      - MAX_PLAYERS={MaxPlayersTextBox.Text}");
            dockerComposeContent.AppendLine($"      - SHOW_ADMIN_COMMANDS_IN_CHAT={ShowAdminCommandsInChatCheckBox.IsChecked.Value.ToString().ToUpper()}");
            dockerComposeContent.AppendLine($"      - CLUSTER_ID={ClusterIdTextBox.Text}");
            dockerComposeContent.AppendLine($"      - MOD_IDS={ModIdsTextBox.Text}");
            dockerComposeContent.AppendLine($"      - PASSIVE_MODS={PassiveModsTextBox.Text}");
            dockerComposeContent.AppendLine($"      - CUSTOM_SERVER_ARGS={CustomServerArgsTextBox.Text}");
            dockerComposeContent.AppendLine("    ports:");
            dockerComposeContent.AppendLine($"      - \"{_asaPort}:{_asaPort}/tcp\"");
            dockerComposeContent.AppendLine($"      - \"{_asaPort}:{_asaPort}/udp\"");
            dockerComposeContent.AppendLine($"      - \"{_rconPort}:{_rconPort}/tcp\"");
            dockerComposeContent.AppendLine("    volumes:");
            dockerComposeContent.AppendLine($"      - \"{serverPath}/ServerFiles/arkserver:/home/pok/arkserver\"");
            dockerComposeContent.AppendLine($"      - \"{instanceDirPath}/Saved:/home/pok/arkserver/ShooterGame/Saved\"");
            dockerComposeContent.AppendLine($"      - \"{basePath}/{selectedCluster.Name}:/home/pok/arkserver/ShooterGame/Saved/clusters\"");
            dockerComposeContent.AppendLine($"    mem_limit: {MemLimitTextBox.Text}");

            await _sshService.WriteFileAsync(dockerComposeFilePath, dockerComposeContent.ToString());

            // Copy config files from defaults to Instance/Saved/Config/WindowsServer
            StatusTextBlock.Text = "Copying config files...";
            string configTargetDir = $"{instanceDirPath}/Saved/Config/WindowsServer";
            string mkdirConfigCommand = $"mkdir -p \"{configTargetDir}\"";
            await _sshService.ExecuteCommandAsync(mkdirConfigCommand);

            // Copy Game.ini and GameUserSettings.ini from defaults folder
            string defaultsPath = $"{serverPath}/defaults";
            string copyGameIniCommand = $"cp \"{defaultsPath}/Game.ini\" \"{configTargetDir}/Game.ini\" 2>/dev/null || true";
            await _sshService.ExecuteCommandAsync(copyGameIniCommand);
            
            string copyGameUserSettingsCommand = $"cp \"{defaultsPath}/GameUserSettings.ini\" \"{configTargetDir}/GameUserSettings.ini\" 2>/dev/null || true";
            await _sshService.ExecuteCommandAsync(copyGameUserSettingsCommand);

            // Hide progress bar
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusTextBlock.Visibility = Visibility.Collapsed;

            // Create .env file
            var envContent = new StringBuilder();
            envContent.AppendLine($"ASA_PORT={_asaPort}");
            envContent.AppendLine($"RCON_PORT={_rconPort}");
            envContent.AppendLine($"BATTLEEYE={BattleEyeCheckBox.IsChecked.Value.ToString().ToUpper()}");
            envContent.AppendLine($"API={ApiCheckBox.IsChecked.Value.ToString().ToUpper()}");
            envContent.AppendLine($"RCON_ENABLED={RconEnabledCheckBox.IsChecked.Value.ToString().ToUpper()}");
            envContent.AppendLine($"DISPLAY_POK_MONITOR_MESSAGE={DisplayPokMonitorMessageCheckBox.IsChecked.Value.ToString().ToUpper()}");
            envContent.AppendLine($"RANDOM_STARTUP_DELAY={RandomStartupDelayCheckBox.IsChecked.Value.ToString().ToUpper()}");
            envContent.AppendLine($"CPU_OPTIMIZATION={CpuOptimizationCheckBox.IsChecked.Value.ToString().ToUpper()}");
            envContent.AppendLine($"UPDATE_SERVER={UpdateServerCheckBox.IsChecked.Value.ToString().ToUpper()}");
            envContent.AppendLine($"ENABLE_MOTD={EnableMotdCheckBox.IsChecked.Value.ToString().ToUpper()}");
            envContent.AppendLine($"MOTD={MotdTextBox.Text}");
            envContent.AppendLine($"MOTD_DURATION={MotdDurationTextBox.Text}");
            envContent.AppendLine($"MAP_NAME={MapNameTextBox.Text}");
            envContent.AppendLine($"SESSION_NAME={SessionNameTextBox.Text}");
            envContent.AppendLine($"SERVER_ADMIN_PASSWORD={ServerAdminPasswordTextBox.Text}");
            envContent.AppendLine($"SERVER_PASSWORD={ServerPasswordTextBox.Text}");
            envContent.AppendLine($"MAX_PLAYERS={MaxPlayersTextBox.Text}");
            envContent.AppendLine($"SHOW_ADMIN_COMMANDS_IN_CHAT={ShowAdminCommandsInChatCheckBox.IsChecked.Value.ToString().ToUpper()}");
            envContent.AppendLine($"CLUSTER_ID={ClusterIdTextBox.Text}");
            envContent.AppendLine($"MOD_IDS={ModIdsTextBox.Text}");
            envContent.AppendLine($"PASSIVE_MODS={PassiveModsTextBox.Text}");
            envContent.AppendLine($"CUSTOM_SERVER_ARGS={CustomServerArgsTextBox.Text}");
            envContent.AppendLine($"mem_limit={MemLimitTextBox.Text}");

            // Write .env file
            await _sshService.WriteFileAsync($"{serverPath}/.env", envContent.ToString());

            MessageBox.Show(
                $"Szerver létrehozva: {serverPath}",
                LocalizationHelper.GetString("success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Hiba a szerver létrehozásakor: {ex.Message}",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            CreateButton.IsEnabled = true;
            CreateButton.Content = LocalizationHelper.GetString("create");
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
