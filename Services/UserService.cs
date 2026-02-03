using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using ZedASAManager.Models;

namespace ZedASAManager.Services;

public class UserService
{
    private readonly string _usersDirectory;
    private readonly string _usersFilePath;
    private readonly EncryptionService _encryptionService;

    public UserService()
    {
        _encryptionService = new EncryptionService();
        
        // Program könyvtár: AppData\Local\ZedASAManager
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZedASAManager");
        
        _usersDirectory = appDataPath;
        _usersFilePath = Path.Combine(_usersDirectory, "users.json");

        // Könyvtár létrehozása, ha nem létezik
        if (!Directory.Exists(_usersDirectory))
        {
            Directory.CreateDirectory(_usersDirectory);
        }
    }

    public bool RegisterUser(string username, string password, string fullName = "", string email = "", string? phoneNumber = null, string? companyName = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            var users = LoadUsers();
            
            // Ellenőrizzük, hogy létezik-e már ilyen felhasználó
            if (users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                return false; // Felhasználó már létezik
            }

            // Ellenőrizzük, hogy létezik-e már ilyen e-mail
            if (!string.IsNullOrWhiteSpace(email) && users.Any(u => !string.IsNullOrEmpty(u.Email) && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                return false; // E-mail már létezik
            }

            var newUser = new User
            {
                Username = username,
                EncryptedPassword = EncryptionService.Encrypt(password),
                FullName = fullName,
                Email = email,
                PhoneNumber = phoneNumber,
                CompanyName = companyName,
                AcceptedTerms = true,
                TermsAcceptedAt = DateTime.Now,
                CreatedAt = DateTime.Now
            };

            users.Add(newUser);
            SaveUsers(users);
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Regisztrációs hiba: {ex.Message}");
            return false;
        }
    }

    public bool LoginUser(string username, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            var users = LoadUsers();
            var user = users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (user == null)
                return false;

            string decryptedPassword = EncryptionService.Decrypt(user.EncryptedPassword ?? string.Empty);
            
            if (decryptedPassword != password)
                return false;

            // Frissítjük a last login dátumot
            user.LastLoginAt = DateTime.Now;
            SaveUsers(users);
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Bejelentkezési hiba: {ex.Message}");
            return false;
        }
    }

    public List<User> LoadUsers()
    {
        try
        {
            if (!File.Exists(_usersFilePath))
                return new List<User>();

            string json = File.ReadAllText(_usersFilePath);
            var users = JsonConvert.DeserializeObject<List<User>>(json);
            return users ?? new List<User>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Felhasználók betöltési hiba: {ex.Message}");
            return new List<User>();
        }
    }

    private void SaveUsers(List<User> users)
    {
        try
        {
            string json = JsonConvert.SerializeObject(users, Formatting.Indented);
            File.WriteAllText(_usersFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Felhasználók mentési hiba: {ex.Message}");
        }
    }

    public string GetUserDataDirectory(string username)
    {
        // Felhasználónként külön könyvtár: AppData\Local\ZedASAManager\{username}
        return Path.Combine(_usersDirectory, username);
    }
}
