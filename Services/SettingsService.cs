using Newtonsoft.Json;
using System.IO;
using ZedASAManager.Models;
using ZedASAManager.Services;

namespace ZedASAManager.Services;

public class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "ZedASAManager");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");
    }

    public class SettingsData
    {
        public ConnectionSettings? ConnectionSettings { get; set; }
        public List<ServerInstance> Servers { get; set; } = new();
    }

    public SettingsData LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                return JsonConvert.DeserializeObject<SettingsData>(json) ?? new SettingsData();
            }
        }
        catch (Exception ex)
        {
            // Log error if needed
            System.Diagnostics.Debug.WriteLine($"Beállítások betöltési hiba: {ex.Message}");
        }

        return new SettingsData();
    }

    public void SaveSettings(SettingsData settings)
    {
        try
        {
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            // Log error if needed
            System.Diagnostics.Debug.WriteLine($"Beállítások mentési hiba: {ex.Message}");
            throw;
        }
    }

    public void SaveConnectionSettings(ConnectionSettings settings)
    {
        var data = LoadSettings();
        data.ConnectionSettings = settings;
        SaveSettings(data);
    }

    public ConnectionSettings? LoadConnectionSettings()
    {
        var settings = LoadSettings().ConnectionSettings;
        
        // Ideiglenes tesztelési adatok - ha nincs mentett beállítás
        if (settings == null)
        {
            settings = new ConnectionSettings
            {
                Host = "65.21.76.239",
                Port = 22,
                Username = "zedinke",
                EncryptedPassword = EncryptionService.Encrypt("geleta"),
                UseSshKey = false,
                ServerBasePath = "/home/zedinke/asa_server"
            };
        }
        else if (string.IsNullOrEmpty(settings.ServerBasePath) && !string.IsNullOrEmpty(settings.Username))
        {
            // Ha nincs beállítva ServerBasePath, de van Username, akkor generáljuk
            settings.ServerBasePath = $"/home/{settings.Username}/asa_server";
        }
        
        return settings;
    }

    public void SaveServers(List<ServerInstance> servers)
    {
        var data = LoadSettings();
        data.Servers = servers;
        SaveSettings(data);
    }

    public List<ServerInstance> LoadServers()
    {
        return LoadSettings().Servers;
    }
}
