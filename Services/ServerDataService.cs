using System.IO;
using Newtonsoft.Json;
using ZedASAManager.Models;

namespace ZedASAManager.Services;

public class ServerDataService
{
    public ServerDataService()
    {
    }

    public List<SavedServer> LoadServers(string username)
    {
        try
        {
            string userDataDir = GetUserDataDirectory(username);
            string serversFilePath = Path.Combine(userDataDir, "servers.json");

            if (!File.Exists(serversFilePath))
                return new List<SavedServer>();

            string json = File.ReadAllText(serversFilePath);
            var servers = JsonConvert.DeserializeObject<List<SavedServer>>(json);
            return servers ?? new List<SavedServer>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Szerverek betöltési hiba: {ex.Message}");
            return new List<SavedServer>();
        }
    }

    public void SaveServers(string username, List<SavedServer> servers)
    {
        try
        {
            string userDataDir = GetUserDataDirectory(username);
            
            // Könyvtár létrehozása, ha nem létezik
            if (!Directory.Exists(userDataDir))
            {
                Directory.CreateDirectory(userDataDir);
            }

            string serversFilePath = Path.Combine(userDataDir, "servers.json");

            // Servers already have encrypted passwords, just save them
            var serversToSave = servers;

            string json = JsonConvert.SerializeObject(serversToSave, Formatting.Indented);
            File.WriteAllText(serversFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Szerverek mentési hiba: {ex.Message}");
        }
    }

    public void AddServer(string username, SavedServer server)
    {
        var servers = LoadServers(username);
        
        // Ha még nincs titkosítva, titkosítjuk
        if (!string.IsNullOrEmpty(server.EncryptedPassword))
        {
            // Ellenőrizzük, hogy már titkosítva van-e (nem lehet dekódolni, akkor már titkosítva van)
            try
            {
                string decrypted = EncryptionService.Decrypt(server.EncryptedPassword);
                // Ha sikeresen dekódolható, akkor még nincs titkosítva, titkosítjuk
                server.EncryptedPassword = EncryptionService.Encrypt(decrypted);
            }
            catch
            {
                // Ha nem lehet dekódolni, akkor már titkosítva van, nem csinálunk semmit
            }
        }
        
        servers.Add(server);
        SaveServers(username, servers);
    }

    public void RemoveServer(string username, string serverName)
    {
        var servers = LoadServers(username);
        servers.RemoveAll(s => s.Name == serverName);
        SaveServers(username, servers);
    }

    public SavedServer? GetServer(string username, string serverName)
    {
        var servers = LoadServers(username);
        return servers.FirstOrDefault(s => s.Name == serverName);
    }

    private string GetUserDataDirectory(string username)
    {
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZedASAManager");
        
        return Path.Combine(appDataPath, username);
    }

    public ConnectionSettings ConvertToConnectionSettings(SavedServer server)
    {
        return new ConnectionSettings
        {
            Host = server.Host,
            Port = server.Port,
            Username = server.Username,
            EncryptedPassword = server.EncryptedPassword,
            SshKeyPath = server.SshKeyPath,
            UseSshKey = server.UseSshKey
        };
    }
}
