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
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            // Try AssemblyInformationalVersion first (this is set by <Version> in .csproj)
            var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (versionAttribute != null && !string.IsNullOrEmpty(versionAttribute.InformationalVersion))
            {
                var version = versionAttribute.InformationalVersion;
                // Remove git commit hash if present (format: "1.0.2+hash")
                if (version.Contains('+'))
                {
                    version = version.Split('+')[0];
                }
                System.Diagnostics.Debug.WriteLine($"GetCurrentVersion: Found AssemblyInformationalVersion: {version}");
                return version;
            }

            // Try AssemblyFileVersion (this is set by <FileVersion> in .csproj)
            var fileVersionAttribute = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            if (fileVersionAttribute != null && !string.IsNullOrEmpty(fileVersionAttribute.Version))
            {
                // Convert "1.0.2.0" to "1.0.2"
                var version = fileVersionAttribute.Version;
                if (version.Split('.').Length == 4)
                {
                    version = string.Join(".", version.Split('.').Take(3));
                }
                System.Diagnostics.Debug.WriteLine($"GetCurrentVersion: Found AssemblyFileVersion: {version}");
                return version;
            }

            // Try Assembly.GetName().Version as fallback
            var assemblyVersion = assembly.GetName().Version;
            if (assemblyVersion != null)
            {
                var version = $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
                System.Diagnostics.Debug.WriteLine($"GetCurrentVersion: Found Assembly.GetName().Version: {version}");
                return version;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetCurrentVersion error: {ex.Message}");
        }

        System.Diagnostics.Debug.WriteLine("GetCurrentVersion: Using fallback version 1.0.0");
        return "1.0.0"; // Fallback
    }

    public async Task<(bool hasUpdate, bool isRequired, string? latestVersion, string? releaseNotes)> CheckForUpdatesAsync()
    {
        try
        {
            var currentVersion = GetCurrentVersion();
            System.Diagnostics.Debug.WriteLine($"Current version: {currentVersion}");
            
            var latestVersionInfo = await GetLatestVersionAsync();

            if (latestVersionInfo == null)
            {
                System.Diagnostics.Debug.WriteLine("No release info found from GitHub");
                LogHelper.WriteToStartupLog("CheckForUpdatesAsync: latestVersionInfo is null - GitHub API call failed or returned no data");
                return (false, false, null, null);
            }

            var latestVersion = latestVersionInfo.Version;
            System.Diagnostics.Debug.WriteLine($"Latest version from GitHub: {latestVersion}");
            LogHelper.WriteToStartupLog($"CheckForUpdatesAsync: latestVersion={latestVersion}, downloadUrl={latestVersionInfo.DownloadUrl}");
            
            var comparison = CompareVersions(latestVersion, currentVersion);
            System.Diagnostics.Debug.WriteLine($"Version comparison result: {comparison} (1 = latest is newer, 0 = same, -1 = latest is older)");
            
            var isRequired = IsUpdateRequired(latestVersion, currentVersion);
            var hasUpdate = comparison > 0;
            
            System.Diagnostics.Debug.WriteLine($"Update check result: hasUpdate={hasUpdate}, isRequired={isRequired}");

            return (hasUpdate, isRequired, latestVersion, latestVersionInfo.ReleaseNotes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update check error: {ex.Message}\n{ex.StackTrace}");
            return (false, false, null, null);
        }
    }

    private async Task<ReleaseInfo?> GetLatestVersionAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
            System.Diagnostics.Debug.WriteLine($"Checking for updates from: {url}");
            
            var response = await _httpClient.GetStringAsync(url);
            System.Diagnostics.Debug.WriteLine($"GitHub API response received: {response.Substring(0, Math.Min(500, response.Length))}...");
            
            var json = JObject.Parse(response);

            var tagName = json["tag_name"]?.ToString();
            System.Diagnostics.Debug.WriteLine($"GitHub API tag_name: {tagName}");
            
            if (string.IsNullOrEmpty(tagName))
            {
                System.Diagnostics.Debug.WriteLine("GitHub API: tag_name is null or empty");
                return null;
            }

            // Remove 'v' prefix if present (e.g., "v1.0.0" -> "1.0.0")
            var version = tagName.StartsWith("v") ? tagName.Substring(1) : tagName;
            System.Diagnostics.Debug.WriteLine($"GitHub API: Parsed version: {version}");

            var body = json["body"]?.ToString() ?? string.Empty;
            var assets = json["assets"] as JArray;
            var downloadUrl = string.Empty;

            System.Diagnostics.Debug.WriteLine($"GitHub API: Found {assets?.Count ?? 0} assets");

            if (assets != null && assets.Count > 0)
            {
                // Find asset matching the pattern
                var assetFileName = AssetFileNamePattern.Replace("{version}", version);
                System.Diagnostics.Debug.WriteLine($"GitHub API: Looking for asset matching pattern: {assetFileName}");
                
                foreach (var asset in assets)
                {
                    var name = asset["name"]?.ToString();
                    System.Diagnostics.Debug.WriteLine($"GitHub API: Checking asset: {name}");
                    
                    if (!string.IsNullOrEmpty(name) && name.Contains(version))
                    {
                        downloadUrl = asset["browser_download_url"]?.ToString() ?? string.Empty;
                        System.Diagnostics.Debug.WriteLine($"GitHub API: Found matching asset: {name}, URL: {downloadUrl}");
                        break;
                    }
                }

                // If no exact match, use first asset
                if (string.IsNullOrEmpty(downloadUrl) && assets.Count > 0)
                {
                    downloadUrl = assets[0]["browser_download_url"]?.ToString() ?? string.Empty;
                    System.Diagnostics.Debug.WriteLine($"GitHub API: Using first asset as fallback: {downloadUrl}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("GitHub API: No assets found in release");
            }

            var releaseInfo = new ReleaseInfo
            {
                Version = version,
                DownloadUrl = downloadUrl,
                ReleaseNotes = body
            };
            
            System.Diagnostics.Debug.WriteLine($"GitHub API: Returning ReleaseInfo - Version: {releaseInfo.Version}, DownloadUrl: {releaseInfo.DownloadUrl}");
            
            return releaseInfo;
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"HTTP Request Error fetching latest version: {httpEx.Message}\nStatus: {httpEx.Data}\n{httpEx.StackTrace}");
            try
            {
                File.AppendAllText("C:\\temp\\zedasa_startup.log", $"GitHub API HTTP Error: {httpEx.Message}\n");
            }
            catch { }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching latest version: {ex.Message}\n{ex.StackTrace}");
            LogHelper.WriteToStartupLog($"GitHub API Error: {ex.Message}\n{ex.StackTrace}");
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

    public async Task<List<ReleaseInfo>> GetAllReleasesAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases";
            System.Diagnostics.Debug.WriteLine($"Fetching all releases from: {url}");
            
            var responseBytes = await _httpClient.GetByteArrayAsync(url);
            var response = System.Text.Encoding.UTF8.GetString(responseBytes);
            var jsonArray = JArray.Parse(response);
            
            var releases = new List<ReleaseInfo>();
            
            foreach (var releaseJson in jsonArray)
            {
                var tagName = releaseJson["tag_name"]?.ToString();
                if (string.IsNullOrEmpty(tagName))
                    continue;
                
                // Remove 'v' prefix if present
                var version = tagName.StartsWith("v") ? tagName.Substring(1) : tagName;
                var body = releaseJson["body"]?.ToString() ?? string.Empty;
                var publishedAt = releaseJson["published_at"]?.ToString() ?? string.Empty;
                
                // Clean up the release notes - remove any problematic characters
                body = System.Text.RegularExpressions.Regex.Replace(body, @"[\u0000-\u001F]", string.Empty);
                
                var releaseInfo = new ReleaseInfo
                {
                    Version = version,
                    ReleaseNotes = body,
                    PublishedAt = publishedAt
                };
                
                releases.Add(releaseInfo);
            }
            
            // Sort by version (newest first)
            releases.Sort((a, b) => -CompareVersions(a.Version, b.Version));
            
            return releases;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching all releases: {ex.Message}\n{ex.StackTrace}");
            return new List<ReleaseInfo>();
        }
    }

    public class ReleaseInfo
    {
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string PublishedAt { get; set; } = string.Empty;
    }
}
