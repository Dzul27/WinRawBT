using InTheHand.Net;

namespace WinRawBT.Models;

/// <summary>
/// Model representing a paired Bluetooth printer (TASK-04)
/// </summary>
public class PairedPrinterItem
{
    public string Name { get; set; } = string.Empty;
    public BluetoothAddress BluetoothAddress { get; set; }
    
    /// <summary>
    /// Formatted MAC address in XX:XX:XX:XX:XX:XX format
    /// </summary>
    public string MacAddress => BluetoothAddress.ToString("X").ToUpper();

    public override string ToString()
    {
        return $"{Name} ({FormatMacAddress(MacAddress)})";
    }

    /// <summary>
    /// Format MAC address as XX:XX:XX:XX:XX:XX (TASK-04)
    /// </summary>
    public static string FormatMacAddress(string mac)
    {
        if (string.IsNullOrEmpty(mac) || mac.Length != 12)
            return mac;

        return $"{mac.Substring(0, 2)}:{mac.Substring(2, 2)}:{mac.Substring(4, 2)}:" +
               $"{mac.Substring(6, 2)}:{mac.Substring(8, 2)}:{mac.Substring(10, 2)}";
    }
}
