using System.Text;
using System.Text.RegularExpressions;
using ZedASAManager.Models;

namespace ZedASAManager.Services;

public class ConfigService
{
    private readonly SshService _sshService;
    private readonly ConfigDescriptionService _descriptionService;
    private const string BasePath = "/home/zedinke/asa_server";

    public ConfigService(SshService sshService)
    {
        _sshService = sshService;
        _descriptionService = new ConfigDescriptionService();
    }

    public string GetConfigPath(string serverName, string configFileName)
    {
        return $"{BasePath}/{serverName}/Instance_{serverName}/Saved/Config/WindowsServer/{configFileName}";
    }

    public async Task<string> ReadConfigFileAsync(string serverName, string configFileName)
    {
        try
        {
            string remotePath = GetConfigPath(serverName, configFileName);
            return await _sshService.ReadFileAsync(remotePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Config fájl olvasási hiba ({configFileName}): {ex.Message}");
            throw;
        }
    }

    public async Task SaveConfigFileAsync(string serverName, string configFileName, string content)
    {
        try
        {
            string remotePath = GetConfigPath(serverName, configFileName);
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
