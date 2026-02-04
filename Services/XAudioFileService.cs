using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ZedASAManager.Utilities;

namespace ZedASAManager.Services;

public class XAudioFileService
{
    private readonly SshService _sshService;
    private readonly HttpClient _httpClient;
    private const string GitHubOwner = "zedinke";
    private const string GitHubRepo = "ZedArkManager";
    private const string FileName = "xaudio2_9.dll";
    private const string RelativePath = "ServerFiles/arkserver/ShooterGame/Binaries/Win64";
    private const string GitHubReleaseTag = "v1.0.16"; // Update this when uploading the file to a new release

    public XAudioFileService(SshService sshService)
    {
        _sshService = sshService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ZedASAManager");
    }

    /// <summary>
    /// Checks if the xaudio2_9.dll file exists in the server's directory
    /// </summary>
    /// <param name="serverDirectoryPath">Full path to the server directory (e.g., /home/user/asa_server/Cluster_name_server)</param>
    /// <returns>True if file exists, false otherwise</returns>
    public async Task<bool> CheckFileExistsAsync(string serverDirectoryPath)
    {
        try
        {
            string filePath = $"{serverDirectoryPath}/{RelativePath}/{FileName}";
            string checkCommand = $"test -f \"{filePath}\" && echo 'exists' || echo 'missing'";
            string result = await _sshService.ExecuteCommandAsync(checkCommand);
            return result.Trim().Contains("exists", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogHelper.WriteToStartupLog($"Error checking xaudio2_9.dll existence: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Downloads the xaudio2_9.dll file from GitHub and copies it to the server
    /// </summary>
    /// <param name="serverDirectoryPath">Full path to the server directory</param>
    /// <returns>True if download and copy succeeded, false otherwise</returns>
    public async Task<bool> DownloadAndCopyFileAsync(string serverDirectoryPath)
    {
        try
        {
            // Download file from GitHub release - try latest first, then specific tag
            string downloadUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest/download/{FileName}";
            LogHelper.WriteToStartupLog($"Downloading xaudio2_9.dll from: {downloadUrl}");

            byte[] fileBytes;
            try
            {
                fileBytes = await _httpClient.GetByteArrayAsync(downloadUrl);
            }
            catch (HttpRequestException ex)
            {
                LogHelper.WriteToStartupLog($"Failed to download xaudio2_9.dll from latest release: {ex.Message}");
                // Try specific tag as fallback
                downloadUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/download/{GitHubReleaseTag}/{FileName}";
                LogHelper.WriteToStartupLog($"Trying specific release tag: {downloadUrl}");
                fileBytes = await _httpClient.GetByteArrayAsync(downloadUrl);
            }

            // Create directory structure on server
            string targetDir = $"{serverDirectoryPath}/{RelativePath}";
            string mkdirCommand = $"mkdir -p \"{targetDir}\"";
            await _sshService.ExecuteCommandAsync(mkdirCommand);

            // Write file directly using SFTP
            string targetFilePath = $"{targetDir}/{FileName}";
            
            try
            {
                await _sshService.WriteBinaryFileAsync(targetFilePath, fileBytes);
                
                // Set file permissions
                string chmodCommand = $"chmod 644 \"{targetFilePath}\"";
                await _sshService.ExecuteCommandAsync(chmodCommand);
                
                LogHelper.WriteToStartupLog($"Successfully copied xaudio2_9.dll to {targetFilePath}");
                return true;
            }
            catch (Exception writeEx)
            {
                LogHelper.WriteToStartupLog($"Failed to write xaudio2_9.dll: {writeEx.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            LogHelper.WriteToStartupLog($"Error downloading/copying xaudio2_9.dll: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Ensures the xaudio2_9.dll file exists in the server directory, downloading it if necessary
    /// </summary>
    /// <param name="serverDirectoryPath">Full path to the server directory</param>
    /// <returns>True if file exists or was successfully downloaded, false otherwise</returns>
    public async Task<bool> EnsureFileExistsAsync(string serverDirectoryPath)
    {
        bool exists = await CheckFileExistsAsync(serverDirectoryPath);
        if (exists)
        {
            return true;
        }

        LogHelper.WriteToStartupLog($"xaudio2_9.dll not found in {serverDirectoryPath}, downloading...");
        return await DownloadAndCopyFileAsync(serverDirectoryPath);
    }
}
