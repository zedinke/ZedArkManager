using System.IO;

namespace ZedASAManager.Utilities;

public static class LogHelper
{
    private static string? _logDirectory;
    private static string? _startupLogPath;
    private static string? _configLogPath;
    private static string? _serverCardLogPath;

    private static string GetLogDirectory()
    {
        if (_logDirectory == null)
        {
            // Use LocalApplicationData instead of hardcoded C:\temp
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logDirectory = Path.Combine(appDataPath, "ZedASAManager", "Logs");
            
            // Create directory if it doesn't exist
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch
            {
                // If we can't create the directory, fall back to temp folder
                _logDirectory = Path.GetTempPath();
            }
        }
        return _logDirectory;
    }

    public static string GetStartupLogPath()
    {
        if (_startupLogPath == null)
        {
            _startupLogPath = Path.Combine(GetLogDirectory(), "zedasa_startup.log");
        }
        return _startupLogPath;
    }

    public static string GetConfigLogPath()
    {
        if (_configLogPath == null)
        {
            _configLogPath = Path.Combine(GetLogDirectory(), "zedasa_config_debug.log");
        }
        return _configLogPath;
    }

    public static string GetServerCardLogPath()
    {
        if (_serverCardLogPath == null)
        {
            _serverCardLogPath = Path.Combine(GetLogDirectory(), "servercard_debug.log");
        }
        return _serverCardLogPath;
    }

    public static void WriteToStartupLog(string message)
    {
        try
        {
            File.AppendAllText(GetStartupLogPath(), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
        }
        catch
        {
            // Silently fail if logging is not possible
        }
    }

    public static void WriteToConfigLog(string message)
    {
        try
        {
            File.AppendAllText(GetConfigLogPath(), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
        }
        catch
        {
            // Silently fail if logging is not possible
        }
    }

    public static void WriteToServerCardLog(string message)
    {
        try
        {
            File.AppendAllText(GetServerCardLogPath(), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
        }
        catch
        {
            // Silently fail if logging is not possible
        }
    }
}
