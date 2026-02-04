using System.Text;
using System.Text.RegularExpressions;
using ZedASAManager.Models;

namespace ZedASAManager.Services;

public class ConfigService
{
    private readonly SshService _sshService;
    private readonly ConfigDescriptionService _descriptionService;
    private string _basePath;

    public ConfigService(SshService sshService, string? basePath = null)
    {
        _sshService = sshService;
        _descriptionService = new ConfigDescriptionService();
        _basePath = basePath ?? "/home/zedinke/asa_server";
    }

    public void SetBasePath(string basePath)
    {
        _basePath = basePath;
    }

    public string GetConfigPath(string serverDirectoryPath, string configFileName)
    {
        // The config path is: {serverDirectoryPath}/Instance_{instanceName}/Saved/Config/WindowsServer/{configFileName}
        // We need to find the Instance_* directory dynamically
        // For now, we'll use a pattern that searches for Instance_* directories
        // This will be resolved in ReadConfigFileAsync and SaveConfigFileAsync
        return $"{serverDirectoryPath}/Instance_*/Saved/Config/WindowsServer/{configFileName}";
    }

    private async Task<string> FindInstanceDirectoryAsync(string serverDirectoryPath)
    {
        try
        {
            // Find Instance_* directories in the server directory
            string findInstanceCommand = $"find \"{serverDirectoryPath}\" -maxdepth 1 -type d -name 'Instance_*' 2>/dev/null | head -1";
            string foundInstancePath = await _sshService.ExecuteCommandAsync(findInstanceCommand);
            
            if (!string.IsNullOrEmpty(foundInstancePath.Trim()))
            {
                return foundInstancePath.Trim();
            }
            
            // Fallback: try to construct path from server directory name
            // Extract the last part of the directory path as instance name
            string instanceName = serverDirectoryPath.Contains('/') 
                ? serverDirectoryPath.Substring(serverDirectoryPath.LastIndexOf('/') + 1) 
                : serverDirectoryPath;
            
            // If the name contains underscores, try to extract the last part
            if (instanceName.Contains('_'))
            {
                string[] parts = instanceName.Split('_');
                if (parts.Length > 1)
                {
                    instanceName = parts[parts.Length - 1];
                }
            }
            
            return $"{serverDirectoryPath}/Instance_{instanceName}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error finding instance directory: {ex.Message}");
            // Fallback to default pattern
            string instanceName = serverDirectoryPath.Contains('/') 
                ? serverDirectoryPath.Substring(serverDirectoryPath.LastIndexOf('/') + 1) 
                : serverDirectoryPath;
            return $"{serverDirectoryPath}/Instance_{instanceName}";
        }
    }

    public async Task<string> ReadConfigFileAsync(string serverDirectoryPath, string configFileName)
    {
        try
        {
            string instanceDir = await FindInstanceDirectoryAsync(serverDirectoryPath);
            string remotePath = $"{instanceDir}/Saved/Config/WindowsServer/{configFileName}";
            System.Diagnostics.Debug.WriteLine($"Reading config file from: {remotePath}");
            return await _sshService.ReadFileAsync(remotePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Config fájl olvasási hiba ({configFileName}): {ex.Message}");
            throw;
        }
    }

    public async Task SaveConfigFileAsync(string serverDirectoryPath, string configFileName, string content)
    {
        try
        {
            string instanceDir = await FindInstanceDirectoryAsync(serverDirectoryPath);
            string remotePath = $"{instanceDir}/Saved/Config/WindowsServer/{configFileName}";
            System.Diagnostics.Debug.WriteLine($"Saving config file to: {remotePath}");
            await _sshService.WriteFileAsync(remotePath, content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Config fájl mentési hiba ({configFileName}): {ex.Message}");
            throw;
        }
    }

    public IniFile ParseIniFile(string content)
    {
        var iniFile = new IniFile();
        var currentSection = new IniSection { Name = "" };
        iniFile.Sections.Add(currentSection);

        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            string trimmedLine = line.Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
            {
                if (!string.IsNullOrEmpty(trimmedLine))
                {
                    currentSection.Lines.Add(new IniLine { Type = IniLineType.Comment, Content = trimmedLine });
                }
                else
                {
                    currentSection.Lines.Add(new IniLine { Type = IniLineType.Empty, Content = "" });
                }
                continue;
            }

            // Check for section header [SectionName]
            var sectionMatch = Regex.Match(trimmedLine, @"^\[([^\]]+)\]$");
            if (sectionMatch.Success)
            {
                currentSection = new IniSection { Name = sectionMatch.Groups[1].Value };
                iniFile.Sections.Add(currentSection);
                continue;
            }

            // Check for key=value
            var keyValueMatch = Regex.Match(trimmedLine, @"^([^=]+)=(.*)$");
            if (keyValueMatch.Success)
            {
                string key = keyValueMatch.Groups[1].Value.Trim();
                string value = keyValueMatch.Groups[2].Value.Trim();
                
                // Check if value is boolean
                bool isBoolean = value.Equals("True", StringComparison.OrdinalIgnoreCase) || 
                                value.Equals("False", StringComparison.OrdinalIgnoreCase);
                
                // Get description for this key
                string description = _descriptionService.GetDescription(key, currentSection.Name);
                
                currentSection.Lines.Add(new IniLine
                {
                    Type = isBoolean ? IniLineType.Boolean : IniLineType.Value,
                    Key = key,
                    Value = value,
                    Content = trimmedLine,
                    Description = description
                });
            }
            else
            {
                // Unknown format, keep as-is
                currentSection.Lines.Add(new IniLine { Type = IniLineType.Unknown, Content = trimmedLine });
            }
        }

        return iniFile;
    }

    public string SerializeIniFile(IniFile iniFile)
    {
        var sb = new StringBuilder();
        
        foreach (var section in iniFile.Sections)
        {
            if (!string.IsNullOrEmpty(section.Name))
            {
                sb.AppendLine($"[{section.Name}]");
            }
            
            foreach (var line in section.Lines)
            {
                switch (line.Type)
                {
                    case IniLineType.Comment:
                    case IniLineType.Empty:
                    case IniLineType.Unknown:
                        sb.AppendLine(line.Content);
                        break;
                    case IniLineType.Boolean:
                    case IniLineType.Value:
                        sb.AppendLine($"{line.Key}={line.Value}");
                        break;
                }
            }
            
            if (!string.IsNullOrEmpty(section.Name))
            {
                sb.AppendLine();
            }
        }
        
        return sb.ToString();
    }
}

public class IniFile
{
    public List<IniSection> Sections { get; set; } = new();
}

public class IniSection
{
    public string Name { get; set; } = "";
    public List<IniLine> Lines { get; set; } = new();
}

public class IniLine
{
    public IniLineType Type { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string Content { get; set; } = "";
    public string Description { get; set; } = "";
}

public enum IniLineType
{
    Empty,
    Comment,
    Section,
    Boolean,
    Value,
    Unknown
}
