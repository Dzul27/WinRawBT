using System.Runtime.Serialization;

namespace WinRawBT.Models;

/// <summary>
/// Persisted application configuration (TASK-03).
/// Serialized to %AppData%\WinRawBT\config.json.
/// </summary>
[DataContract]
public sealed class AppConfig
{
    /// <summary>MAC of the last selected printer, formatted XX:XX:XX:XX:XX:XX. Null if none chosen yet.</summary>
    [DataMember]
    public string? LastPrinterMac { get; set; }

    /// <summary>Local bridge listener port.</summary>
    [DataMember]
    public int Port { get; set; } = 8080;

    /// <summary>Automatically start the print bridge service on launch when a printer is saved.</summary>
    [DataMember]
    public bool AutoStartService { get; set; }

    /// <summary>Hide to the system tray instead of exiting when the window is closed.</summary>
    [DataMember]
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>Register the app in the Windows startup (Run) registry key.</summary>
    [DataMember]
    public bool RunOnStartup { get; set; }

    /// <summary>
    /// Prepend ESC @ (initialize printer) to jobs that do not already start with it. Off by default:
    /// payloads are sent exactly as the POS produced them. Edit config.json and restart the app to change it.
    /// </summary>
    [DataMember]
    public bool PrependInit { get; set; }

    /// <summary>
    /// Paper width in millimetres, 58 or 80. Only sizes the Test Connection receipt (32 or 48 columns);
    /// jobs from the POS are always sent untouched, so their layout is the POS's responsibility.
    /// </summary>
    [DataMember]
    public int PaperWidthMm { get; set; } = 58;

    /// <summary>
    /// Browser origins allowed to use the bridge, e.g. "https://pos.example.com". Only consulted when
    /// <see cref="AllowAnyOrigin"/> is false; then unlisted web POS apps are rejected (403).
    /// Requests without an Origin header (curl, native apps) are always accepted.
    /// Edit config.json and restart the service to change it.
    /// </summary>
    [DataMember]
    public List<string> AllowedOrigins { get; set; } = new();

    /// <summary>
    /// Accept any browser origin. On by default so a web POS works without configuration. Set to false in
    /// config.json (and list sites in <see cref="AllowedOrigins"/>) to stop other websites open on this PC from printing.
    /// Edit config.json and restart the service to change it.
    /// </summary>
    [DataMember]
    public bool AllowAnyOrigin { get; set; } = true;

    // DataContract deserialization skips constructors and field initializers.
    [OnDeserializing]
    private void ApplyDefaults(StreamingContext _)
    {
        Port = 8080;
        MinimizeToTray = true;
        PaperWidthMm = 58;
        AllowedOrigins = new List<string>();
        AllowAnyOrigin = true;
    }

    [OnDeserialized]
    private void ValidatePort(StreamingContext _)
    {
        if (Port < 1024 || Port > 65535) Port = 8080;
        if (PaperWidthMm != 58 && PaperWidthMm != 80) PaperWidthMm = 58;
    }
}
