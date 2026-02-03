using Newtonsoft.Json;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace ZedASAManager.Utilities;

public class LocalizationHelper
{
    private static Dictionary<string, Dictionary<string, string>>? _translations;
    private static string _currentLanguage = "hu";

    static LocalizationHelper()
    {
        LoadTranslations();
    }

    private static void LoadTranslations()
    {
        _translations = new Dictionary<string, Dictionary<string, string>>();

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var basePath = Path.GetDirectoryName(assembly.Location);
            
            if (string.IsNullOrEmpty(basePath))
            {
                basePath = AppDomain.CurrentDomain.BaseDirectory;
            }

            if (string.IsNullOrEmpty(basePath))
            {
                System.Diagnostics.Debug.WriteLine("Nem található base path a lokalizáció betöltéséhez");
                return;
            }

            var localizationPath = Path.Combine(basePath, "Localization");

            if (!Directory.Exists(localizationPath))
            {
                System.Diagnostics.Debug.WriteLine($"Lokalizációs mappa nem található: {localizationPath}");
                // Initialize with empty translations to prevent null reference
                _translations["hu"] = new Dictionary<string, string>();
                _translations["en"] = new Dictionary<string, string>();
                return;
            }

            var files = Directory.GetFiles(localizationPath, "*.json");
            if (files.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"Nincs lokalizációs fájl a mappában: {localizationPath}");
                _translations["hu"] = new Dictionary<string, string>();
                _translations["en"] = new Dictionary<string, string>();
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    string language = Path.GetFileNameWithoutExtension(file);
                    string json = File.ReadAllText(file);
                    var translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    
                    if (translations != null)
                    {
                        _translations[language] = translations;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Hiba a {file} fájl betöltésekor: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lokalizáció betöltési hiba: {ex.Message}");
            // Initialize with empty translations to prevent null reference
            _translations = new Dictionary<string, Dictionary<string, string>>
            {
                { "hu", new Dictionary<string, string>() },
                { "en", new Dictionary<string, string>() }
            };
        }
    }

    public static void SetLanguage(string languageCode)
    {
        _currentLanguage = languageCode;
    }

    public static string GetString(string key)
    {
        if (_translations == null)
        {
            LoadTranslations();
        }

        if (_translations != null && _translations.TryGetValue(_currentLanguage, out var langDict))
        {
            if (langDict.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        // Fallback to English
        if (_translations != null && _translations.TryGetValue("en", out var enDict))
        {
            if (enDict.TryGetValue(key, out var enValue))
            {
                return enValue;
            }
        }

        // Final fallback: return the key
        return key;
    }

    public static void SetCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        
        string langCode = culture.TwoLetterISOLanguageName;
        SetLanguage(langCode == "hu" ? "hu" : "en");
    }
}
