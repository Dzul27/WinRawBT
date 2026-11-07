using System.Text.Json.Serialization;

namespace WinRawBT.Models;

/// <summary>
/// Application configuration model (TASK-03)
/// </summary>
public class AppConfig
{
    [JsonPropertyName("lastPrinterMac")]
    public string? LastPrinterMac { get; set; }

    [JsonPropertyName("lastPrinterName")]
    public string? LastPrinterName { get; set; }

    [JsonPropertyName("port")]
    public int Port { get; set; } = 40213;

    [JsonPropertyName("autoStartService")]
    public bool AutoStartService { get; set; } = false;

    [JsonPropertyName("minimizeToTray")]
    public bool MinimizeToTray { get; set; } = true;

    [JsonPropertyName("runOnStartup")]
    public bool RunOnStartup { get; set; } = false;

    [JsonPropertyName("selectedPrinterMac")]
    public string? SelectedPrinterMac { get; set; }
}
