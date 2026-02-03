using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZedASAManager.Utilities;

namespace ZedASAManager.Services;

public class UpdateService
{
    private readonly HttpClient _httpClient;
    // GitHub repository owner (username or organization name)
    private const string GitHubOwner = "zedinke";
    // GitHub repository name
    private const string GitHubRepo = "ZedArkManager";
    // Pattern for release asset filename (e.g., "ZedASAManager-1.0.0.zip")
    // The {version} placeholder will be replaced with the actual version number
    private const string AssetFileNamePattern = "ZedASAManager-{version}.zip";

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ZedASAManager-UpdateService");
    }

    public string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (versionAttribute != null && !string.IsNullOrEmpty(versionAttribute.InformationalVersion))
        {
            return versionAttribute.InformationalVersion;
        }

        var fileVersionAttribute = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
        if (fileVersionAttribute != null && !string.IsNullOrEmpty(fileVersionAttribute.Version))
        {
            // Convert "1.0.0.0" to "1.0.0"
            var version = fileVersionAttribute.Version;
            if (version.Split('.').Length == 4)
            {
                version = string.Join(".", version.Split('.').Take(3));
            }
            return version;
        }

        return "1.0.0"; // Fallback
    }

    public async Task<(bool hasUpdate, bool isRequired, string? latestVersion, string? releaseNotes)> CheckForUpdatesAsync()
    {
        try
        {
            var currentVersion = GetCurrentVersion();
            var latestVersionInfo = await GetLatestVersionAsync();

            if (latestVersionInfo == null)
            {
                return (false, false, null, null);
            }

            var latestVersion = latestVersionInfo.Version;
            var isRequired = IsUpdateRequired(latestVersion, currentVersion);
            var hasUpdate = CompareVersions(latestVersion, currentVersion) > 0;

            return (hasUpdate, isRequired, latestVersion, latestVersionInfo.ReleaseNotes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update check error: {ex.Message}");
            return (false, false, null, null);
        }
    }

    private async Task<ReleaseInfo?> GetLatestVersionAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
            var response = await _httpClient.GetStringAsync(url);
            var json = JObject.Parse(response);

            var tagName = json["tag_name"]?.ToString();
            if (string.IsNullOrEmpty(tagName))
            {
                return null;
            }

            // Remove 'v' prefix if present (e.g., "v1.0.0" -> "1.0.0")
            var version = tagName.StartsWith("v") ? tagName.Substring(1) : tagName;

            var body = json["body"]?.ToString() ?? string.Empty;
            var assets = json["assets"] as JArray;
            var downloadUrl = string.Empty;

            if (assets != null && assets.Count > 0)
            {
                // Find asset matching the pattern
                var assetFileName = AssetFileNamePattern.Replace("{version}", version);
                foreach (var asset in assets)
                {
                    var name = asset["name"]?.ToString();
                    if (!string.IsNullOrEmpty(name) && name.Contains(version))
                    {
                        downloadUrl = asset["browser_download_url"]?.ToString() ?? string.Empty;
                        break;
                    }
                }

                // If no exact match, use first asset
                if (string.IsNullOrEmpty(downloadUrl) && assets.Count > 0)
                {
                    downloadUrl = assets[0]["browser_download_url"]?.ToString() ?? string.Empty;
                }
            }

            return new ReleaseInfo
            {
                Version = version,
                DownloadUrl = downloadUrl,
                ReleaseNotes = body
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching latest version: {ex.Message}");
            return null;
        }
    }

    private bool IsUpdateRequired(string latestVersion, string currentVersion)
    {
        // For now, all updates are required
        // Can be modified to only require major version updates
        return CompareVersions(latestVersion, currentVersion) > 0;
    }

    private int CompareVersions(string version1, string version2)
    {
        var v1Parts = version1.Split('.').Select(int.Parse).ToArray();
        var v2Parts = version2.Split('.').Select(int.Parse).ToArray();

        var maxLength = Math.Max(v1Parts.Length, v2Parts.Length);
        Array.Resize(ref v1Parts, maxLength);
        Array.Resize(ref v2Parts, maxLength);

        for (int i = 0; i < maxLength; i++)
        {
            var v1Part = i < v1Parts.Length ? v1Parts[i] : 0;
            var v2Part = i < v2Parts.Length ? v2Parts[i] : 0;

            if (v1Part > v2Part) return 1;
            if (v1Part < v2Part) return -1;
        }

        return 0;
    }

    public async Task<string> DownloadUpdateAsync(string version, IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report(LocalizationHelper.GetString("downloading_update"));

            var releaseInfo = await GetLatestVersionAsync();
            if (releaseInfo == null || string.IsNullOrEmpty(releaseInfo.DownloadUrl))
            {
                throw new Exception("Download URL not found");
            }

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var updatesFolder = Path.Combine(appDataPath, "ZedASAManager", "updates");
            Directory.CreateDirectory(updatesFolder);

            var zipFileName = $"ZedASAManager-{version}.zip";
            var zipPath = Path.Combine(updatesFolder, zipFileName);

            using (var response = await _httpClient.GetAsync(releaseInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                using (var fileStream = new FileStream(zipPath, FileMode.Create))
                using (var httpStream = await response.Content.ReadAsStreamAsync())
                {
                    await httpStream.CopyToAsync(fileStream);
                }
            }

            return zipPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Download error: {ex.Message}");
            throw;
        }
    }

    public async Task<string> ExtractAndBackupAsync(string zipPath, IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report(LocalizationHelper.GetString("backing_up_files"));

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var backupsFolder = Path.Combine(appDataPath, "ZedASAManager", "backups");
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFolder = Path.Combine(backupsFolder, timestamp);
            Directory.CreateDirectory(backupFolder);

            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Backup all files from application directory
            foreach (var file in Directory.GetFiles(appDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(file);
                if (fileName == "ZedASAManager.exe" || fileName.EndsWith(".dll") || fileName.EndsWith(".json"))
                {
                    var destPath = Path.Combine(backupFolder, fileName);
                    File.Copy(file, destPath, true);
                }
            }

            progress?.Report(LocalizationHelper.GetString("extracting_update"));

            // Extract ZIP to temporary folder
            var extractFolder = Path.Combine(Path.GetTempPath(), $"ZedASAManager-Update-{timestamp}");
            Directory.CreateDirectory(extractFolder);

            ZipFile.ExtractToDirectory(zipPath, extractFolder);

            return extractFolder;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Extract/Backup error: {ex.Message}");
            throw;
        }
    }

    public async Task ApplyUpdateAsync(string extractFolder, IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report(LocalizationHelper.GetString("applying_update"));

            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? 
                         Path.Combine(appDirectory, "ZedASAManager.exe");

            // Create update script
            var scriptPath = Path.Combine(Path.GetTempPath(), "ZedASAManager-Update.bat");
            var scriptContent = $@"@echo off
timeout /t 2 /nobreak >nul
xcopy /Y /E /I ""{extractFolder}\*"" ""{appDirectory}""
start """" ""{exePath}""
del ""{scriptPath}""
";

            await File.WriteAllTextAsync(scriptPath, scriptContent);

            // Start the update script
            var processStartInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(processStartInfo);

            // Close current application
            await Task.Delay(500);
            Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Apply update error: {ex.Message}");
            throw;
        }
    }

    private class ReleaseInfo
    {
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
    }
}
