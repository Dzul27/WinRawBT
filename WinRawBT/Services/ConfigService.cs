using System;
using System.IO;
using System.Text.Json;
using WinRawBT.Models;

namespace WinRawBT.Services;

/// <summary>
/// Local Configuration Manager (TASK-03)
/// Handles saving/loading application configuration from %AppData%\WinRawBT\config.json
/// </summary>
public class ConfigService
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinRawBT"
    );
    private static readonly string ConfigFilePath = Path.Combine(AppDataPath, "config.json");

    private AppConfig _config;

    public ConfigService()
    {
        _config = Load();
    }

    /// <summary>
    /// Get current application configuration
    /// </summary>
    public AppConfig Config => _config;

    /// <summary>
    /// Load configuration from file (TASK-03)
    /// </summary>
    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return config ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash - return defaults
            System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
        }

        // Return default configuration if file doesn't exist or error occurred
        return new AppConfig();
    }

    /// <summary>
    /// Save configuration to file (TASK-03)
    /// </summary>
    public void Save()
    {
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }

            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Update last selected printer
    /// </summary>
    public void UpdateLastPrinter(string name, string mac)
    {
        _config.LastPrinterName = name;
        _config.LastPrinterMac = mac;
        Save();
    }

    /// <summary>
    /// Update service settings
    /// </summary>
    public void UpdateSettings(bool autoStart, bool minimizeToTray, bool runOnStartup)
    {
        _config.AutoStartService = autoStart;
        _config.MinimizeToTray = minimizeToTray;
        _config.RunOnStartup = runOnStartup;
        Save();
    }
}
