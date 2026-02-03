using System.IO;
using Newtonsoft.Json;
using ZedASAManager.Utilities;

namespace ZedASAManager.Services;

public class RememberMeService
{
    private readonly string _rememberMeFilePath;
    private const int RememberMeHours = 72;

    public RememberMeService()
    {
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZedASAManager");
        
        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }

        _rememberMeFilePath = Path.Combine(appDataPath, "rememberme.json");
    }

    public class RememberMeData
    {
        public string EncryptedUsername { get; set; } = string.Empty;
        public string EncryptedPassword { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
    }

    public void SaveCredentials(string username, string password)
    {
        try
        {
            var data = new RememberMeData
            {
                EncryptedUsername = EncryptionService.Encrypt(username),
                EncryptedPassword = EncryptionService.Encrypt(password),
                SavedAt = DateTime.Now
            };

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(_rememberMeFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Remember me mentési hiba: {ex.Message}");
        }
    }

    public (string? username, string? password) LoadCredentials()
    {
        try
        {
            if (!File.Exists(_rememberMeFilePath))
                return (null, null);

            string json = File.ReadAllText(_rememberMeFilePath);
            var data = JsonConvert.DeserializeObject<RememberMeData>(json);

            if (data == null)
                return (null, null);

            // Ellenőrizzük, hogy nem telt el 72 óra
            TimeSpan elapsed = DateTime.Now - data.SavedAt;
            if (elapsed.TotalHours > RememberMeHours)
            {
                // Töröljük a fájlt, ha lejárt
                try
                {
                    File.Delete(_rememberMeFilePath);
                }
                catch { }
                return (null, null);
            }

            string username = EncryptionService.Decrypt(data.EncryptedUsername);
            string password = EncryptionService.Decrypt(data.EncryptedPassword);

            return (username, password);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Remember me betöltési hiba: {ex.Message}");
            return (null, null);
        }
    }

    public void ClearCredentials()
    {
        try
        {
            if (File.Exists(_rememberMeFilePath))
            {
                File.Delete(_rememberMeFilePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Remember me törlési hiba: {ex.Message}");
        }
    }
}
