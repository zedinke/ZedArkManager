using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using ZedASAManager.Services;
using ZedASAManager.Utilities;

namespace ZedASAManager.Views;

public partial class DockerSetupWindow : Window
{
    private readonly SshService _sshService;
    private readonly string _serverDirectoryPath;
    private readonly string _instanceName;
    private readonly string _dockerComposeFilePath;
    private readonly ObservableCollection<MapInfo> _maps = new();

    public DockerSetupWindow(SshService sshService, string serverDirectoryPath, string instanceName)
    {
        InitializeComponent();
        _sshService = sshService;
        _serverDirectoryPath = serverDirectoryPath;
        _instanceName = instanceName;
        
        // Find docker-compose file
        string instanceDirPath = $"{_serverDirectoryPath}/Instance_{_instanceName}";
        _dockerComposeFilePath = $"{instanceDirPath}/docker-compose-{_instanceName}.yaml";
        
        InitializeMaps();
        LoadLocalizedStrings();
        _ = LoadDockerComposeAsync();
    }

    private void InitializeMaps()
    {
        _maps.Clear();
        _maps.Add(new MapInfo { DisplayName = "The Island", Value = "TheIsland" });
        _maps.Add(new MapInfo { DisplayName = "The Center", Value = "TheCenter" });
        _maps.Add(new MapInfo { DisplayName = "Scorched Earth", Value = "ScorchedEarth" });
        _maps.Add(new MapInfo { DisplayName = "Aberration", Value = "Aberration" });
        _maps.Add(new MapInfo { DisplayName = "Extinction", Value = "Extinction" });
        _maps.Add(new MapInfo { DisplayName = "Valguero", Value = "Valhuero" });
        _maps.Add(new MapInfo { DisplayName = "Astraeos", Value = "Astraeos" });
        _maps.Add(new MapInfo { DisplayName = "Lost Colony", Value = "LostColony" });
        _maps.Add(new MapInfo { DisplayName = "Ragnarok", Value = "Ragnarok" });
        _maps.Add(new MapInfo { DisplayName = "GigArk Island", Value = "gigarkisland_WP" });
        _maps.Add(new MapInfo { DisplayName = "Other", Value = "Other" });
        
        MapNameComboBox.ItemsSource = _maps;
    }

    private void MapNameComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (MapNameComboBox.SelectedItem is MapInfo selectedMap)
        {
            if (selectedMap.Value == "Other")
            {
                CustomMapNameTextBox.Visibility = Visibility.Visible;
                CustomMapNameTextBox.Text = "";
            }
            else
            {
                CustomMapNameTextBox.Visibility = Visibility.Collapsed;
                CustomMapNameTextBox.Text = "";
            }
        }
    }

    private void LoadLocalizedStrings()
    {
        Title = LocalizationHelper.GetString("docker_setup");
        TitleTextBlock.Text = $"{LocalizationHelper.GetString("docker_setup")}: {_instanceName}";
        MotdLabel.Text = LocalizationHelper.GetString("motd") + ":";
        MotdDurationLabel.Text = LocalizationHelper.GetString("motd_duration") + ":";
        MapNameLabel.Text = LocalizationHelper.GetString("map_name") + ":";
        SessionNameLabel.Text = LocalizationHelper.GetString("session_name") + ":";
        ServerAdminPasswordLabel.Text = LocalizationHelper.GetString("server_admin_password") + ":";
        ServerPasswordLabel.Text = LocalizationHelper.GetString("server_password") + ":";
        MaxPlayersLabel.Text = LocalizationHelper.GetString("max_players") + ":";
        ClusterIdLabel.Text = LocalizationHelper.GetString("cluster_id") + ":";
        ModIdsLabel.Text = LocalizationHelper.GetString("mod_ids") + ":";
        PassiveModsLabel.Text = LocalizationHelper.GetString("passive_mods") + ":";
        CustomServerArgsLabel.Text = LocalizationHelper.GetString("custom_server_args") + ":";
        MemLimitLabel.Text = LocalizationHelper.GetString("mem_limit") + ":";
        SaveButton.Content = LocalizationHelper.GetString("save");
        CancelButton.Content = LocalizationHelper.GetString("cancel");
    }

    private async Task LoadDockerComposeAsync()
    {
        try
        {
            ProgressBar.Visibility = Visibility.Visible;
            StatusTextBlock.Text = LocalizationHelper.GetString("loading") + "...";
            StatusTextBlock.Visibility = Visibility.Visible;

            // Read docker-compose file
            string yamlContent = await _sshService.ReadFileAsync(_dockerComposeFilePath);
            
            // Parse the yaml content
            ParseYamlContent(yamlContent);

            ProgressBar.Visibility = Visibility.Collapsed;
            StatusTextBlock.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Hiba a docker-compose fájl betöltésekor: {ex.Message}",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private void ParseYamlContent(string yamlContent)
    {
        var lines = yamlContent.Split('\n');
        bool inEnvironment = false;

        foreach (var line in lines)
        {
            string trimmedLine = line.Trim();
            
            if (trimmedLine.StartsWith("environment:"))
            {
                inEnvironment = true;
                continue;
            }
            
            if (inEnvironment && trimmedLine.StartsWith("-"))
            {
                // Parse environment variables
                var match = Regex.Match(trimmedLine, @"-?\s*(\w+)[=:](.+)");
                if (match.Success)
                {
                    string key = match.Groups[1].Value;
                    string value = match.Groups[2].Value.Trim();

                    switch (key)
                    {
                        case "BATTLEEYE":
                            BattleEyeCheckBox.IsChecked = value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "API":
                            ApiCheckBox.IsChecked = value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "RCON_ENABLED":
                            RconEnabledCheckBox.IsChecked = value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "DISPLAY_POK_MONITOR_MESSAGE":
                            DisplayPokMonitorMessageCheckBox.IsChecked = value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "RANDOM_STARTUP_DELAY":
                            RandomStartupDelayCheckBox.IsChecked = value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "CPU_OPTIMIZATION":
                            CpuOptimizationCheckBox.IsChecked = value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "UPDATE_SERVER":
                            UpdateServerCheckBox.IsChecked = value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "ENABLE_MOTD":
                            EnableMotdCheckBox.IsChecked = value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "MOTD":
                            MotdTextBox.Text = value;
                            break;
                        case "MOTD_DURATION":
                            MotdDurationTextBox.Text = value;
                            break;
                        case "MAP_NAME":
                            var map = _maps.FirstOrDefault(m => m.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
                            if (map != null)
                            {
                                MapNameComboBox.SelectedItem = map;
                            }
                            else
                            {
                                // Custom map name
                                var otherMap = _maps.FirstOrDefault(m => m.Value == "Other");
                                if (otherMap != null)
                                {
                                    MapNameComboBox.SelectedItem = otherMap;
                                    CustomMapNameTextBox.Text = value;
                                    CustomMapNameTextBox.Visibility = Visibility.Visible;
                                }
                            }
                            break;
                        case "SESSION_NAME":
                            SessionNameTextBox.Text = value;
                            break;
                        case "SERVER_ADMIN_PASSWORD":
                            ServerAdminPasswordTextBox.Text = value;
                            break;
                        case "SERVER_PASSWORD":
                            ServerPasswordTextBox.Text = value;
                            break;
                        case "MAX_PLAYERS":
                            MaxPlayersTextBox.Text = value;
                            break;
                        case "SHOW_ADMIN_COMMANDS_IN_CHAT":
                            ShowAdminCommandsInChatCheckBox.IsChecked = value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "CLUSTER_ID":
                            ClusterIdTextBox.Text = value;
                            break;
                        case "MOD_IDS":
                            ModIdsTextBox.Text = value;
                            break;
                        case "PASSIVE_MODS":
                            PassiveModsTextBox.Text = value;
                            break;
                        case "CUSTOM_SERVER_ARGS":
                            CustomServerArgsTextBox.Text = value;
                            break;
                    }
                }
            }
            else if (trimmedLine.StartsWith("mem_limit:"))
            {
                var match = Regex.Match(trimmedLine, @"mem_limit:\s*(.+)");
                if (match.Success)
                {
                    MemLimitTextBox.Text = match.Groups[1].Value.Trim();
                }
            }
            
            // Check if we've left the environment section
            if (inEnvironment && !trimmedLine.StartsWith("-") && !trimmedLine.StartsWith("#") && !string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith(" "))
            {
                inEnvironment = false;
            }
        }
    }

    private string GetSelectedMapName()
    {
        if (MapNameComboBox.SelectedItem is MapInfo selectedMap)
        {
            if (selectedMap.Value == "Other")
            {
                return CustomMapNameTextBox.Text.Trim();
            }
            return selectedMap.Value;
        }
        return "Aberration"; // Default fallback
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveButton.IsEnabled = false;
            SaveButton.Content = LocalizationHelper.GetString("loading") + "...";
            ProgressBar.Visibility = Visibility.Visible;
            StatusTextBlock.Text = LocalizationHelper.GetString("saving") + "...";
            StatusTextBlock.Visibility = Visibility.Visible;

            // Read the original file to preserve structure
            string originalContent = await _sshService.ReadFileAsync(_dockerComposeFilePath);
            
            // Parse to get ports and other non-editable fields
            int asaPort = ExtractPort(originalContent, "ASA_PORT");
            int rconPort = ExtractPort(originalContent, "RCON_PORT");
            
            // Build new docker-compose content
            var dockerComposeContent = new StringBuilder();
            dockerComposeContent.AppendLine("version: '2.4'");
            dockerComposeContent.AppendLine("");
            dockerComposeContent.AppendLine("services:");
            dockerComposeContent.AppendLine("  asaserver:");
            dockerComposeContent.AppendLine("    build: .");
            dockerComposeContent.AppendLine("    image: acekorneya/asa_server:2_1_latest");
            dockerComposeContent.AppendLine($"    container_name: asa_{_instanceName}");
            dockerComposeContent.AppendLine("    restart: unless-stopped");
            dockerComposeContent.AppendLine("    environment:");
            dockerComposeContent.AppendLine($"      - INSTANCE_NAME={_instanceName}");
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
            string mapName = GetSelectedMapName();
            dockerComposeContent.AppendLine($"      - MAP_NAME={mapName}");
            dockerComposeContent.AppendLine($"      - SESSION_NAME={SessionNameTextBox.Text}");
            dockerComposeContent.AppendLine($"      - SERVER_ADMIN_PASSWORD={ServerAdminPasswordTextBox.Text}");
            dockerComposeContent.AppendLine($"      - SERVER_PASSWORD={ServerPasswordTextBox.Text}");
            dockerComposeContent.AppendLine($"      - ASA_PORT={asaPort}");
            dockerComposeContent.AppendLine($"      - RCON_PORT={rconPort}");
            dockerComposeContent.AppendLine($"      - MAX_PLAYERS={MaxPlayersTextBox.Text}");
            dockerComposeContent.AppendLine($"      - SHOW_ADMIN_COMMANDS_IN_CHAT={ShowAdminCommandsInChatCheckBox.IsChecked.Value.ToString().ToUpper()}");
            dockerComposeContent.AppendLine($"      - CLUSTER_ID={ClusterIdTextBox.Text}");
            dockerComposeContent.AppendLine($"      - MOD_IDS={ModIdsTextBox.Text}");
            dockerComposeContent.AppendLine($"      - PASSIVE_MODS={PassiveModsTextBox.Text}");
            dockerComposeContent.AppendLine($"      - CUSTOM_SERVER_ARGS={CustomServerArgsTextBox.Text}");
            dockerComposeContent.AppendLine("    ports:");
            dockerComposeContent.AppendLine($"      - \"{asaPort}:{asaPort}/tcp\"");
            dockerComposeContent.AppendLine($"      - \"{asaPort}:{asaPort}/udp\"");
            dockerComposeContent.AppendLine($"      - \"{rconPort}:{rconPort}/tcp\"");
            dockerComposeContent.AppendLine("    volumes:");
            string instanceDirPath = $"{_serverDirectoryPath}/Instance_{_instanceName}";
            dockerComposeContent.AppendLine($"      - \"{_serverDirectoryPath}/ServerFiles/arkserver:/home/pok/arkserver\"");
            dockerComposeContent.AppendLine($"      - \"{instanceDirPath}/Saved:/home/pok/arkserver/ShooterGame/Saved\"");
            
            // Extract cluster path from original file
            string clusterPath = ExtractClusterPath(originalContent);
            if (!string.IsNullOrEmpty(clusterPath))
            {
                dockerComposeContent.AppendLine($"      - \"{clusterPath}:/home/pok/arkserver/ShooterGame/Saved/clusters\"");
            }
            
            dockerComposeContent.AppendLine($"    mem_limit: {MemLimitTextBox.Text}");

            await _sshService.WriteFileAsync(_dockerComposeFilePath, dockerComposeContent.ToString());

            ProgressBar.Visibility = Visibility.Collapsed;
            StatusTextBlock.Visibility = Visibility.Collapsed;

            MessageBox.Show(
                LocalizationHelper.GetString("docker_setup_saved"),
                LocalizationHelper.GetString("success"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Hiba a docker-compose fájl mentésekor: {ex.Message}",
                LocalizationHelper.GetString("error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
            SaveButton.Content = LocalizationHelper.GetString("save");
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private int ExtractPort(string yamlContent, string portName)
    {
        var match = Regex.Match(yamlContent, $@"-?\s*{portName}[=:](\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int port))
        {
            return port;
        }
        return 0;
    }

    private string ExtractClusterPath(string yamlContent)
    {
        var match = Regex.Match(yamlContent, @"-?\s*""([^""]+):/home/pok/arkserver/ShooterGame/Saved/clusters""");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return string.Empty;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
