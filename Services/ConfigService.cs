using System.Runtime.Serialization.Json;
using System.Text;
using WinRawBT.Models;

namespace WinRawBT.Services;

/// <summary>
/// Loads and persists <see cref="AppConfig"/> to %AppData%\WinRawBT\config.json (TASK-03).
/// Defaults gracefully when the file is missing or corrupt.
/// </summary>
public sealed class ConfigService
{
    private static readonly DataContractJsonSerializer Serializer = new(typeof(AppConfig));

    private readonly string _configDir;
    private readonly string _configPath;

    public ConfigService()
    {
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinRawBT");
        _configPath = Path.Combine(_configDir, "config.json");
    }

    public string ConfigPath => _configPath;

    /// <summary>
    /// Load config from disk. Returns a default config if the file does not exist
    /// or cannot be parsed, never throwing.
    /// </summary>
    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return new AppConfig();
            }

            string json = File.ReadAllText(_configPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppConfig();
            }

            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return Serializer.ReadObject(ms) as AppConfig ?? new AppConfig();
        }
        catch
        {
            // Corrupt/unreadable config -> fall back to defaults.
            return new AppConfig();
        }
    }

    /// <summary>Persist config to disk, creating the directory if needed.</summary>
    public void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            using var ms = new MemoryStream();
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, ownsStream: false, indent: true))
            {
                Serializer.WriteObject(writer, config);
            }

            File.WriteAllBytes(_configPath, ms.ToArray());
        }
        catch
        {
            // Best-effort persistence; swallow IO errors so the UI stays responsive.
        }
    }
}
