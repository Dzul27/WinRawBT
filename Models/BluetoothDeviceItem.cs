namespace WinRawBT.Models;

/// <summary>
/// A Bluetooth device shown in the printer dropdown (TASK-04, TASK-18): either already
/// paired in Windows or discovered nearby by a scan and not yet paired.
/// </summary>
public sealed class BluetoothDeviceItem
{
    /// <summary>Placeholder shown for devices that did not report a name.</summary>
    public const string UnnamedText = "(unnamed)";

    public BluetoothDeviceItem(string? name, string bluetoothAddress, bool isPaired = true)
    {
        Name = string.IsNullOrWhiteSpace(name) ? UnnamedText : name!;
        BluetoothAddress = bluetoothAddress;
        IsPaired = isPaired;
    }

    /// <summary>Human-readable device name reported by Windows, or "(unnamed)".</summary>
    public string Name { get; }

    /// <summary>MAC address formatted as XX:XX:XX:XX:XX:XX.</summary>
    public string BluetoothAddress { get; }

    /// <summary>True when Windows has the device paired/authenticated.</summary>
    public bool IsPaired { get; }

    /// <summary>True when the device did not report a usable name.</summary>
    public bool IsUnnamed => Name == UnnamedText;

    /// <summary>Copy of this item flagged as paired (used after a successful pairing).</summary>
    public BluetoothDeviceItem AsPaired() => new(Name, BluetoothAddress, isPaired: true);

    /// <summary>Text rendered inside the ComboBox.</summary>
    public override string ToString() =>
        IsPaired ? $"{Name} ({BluetoothAddress})" : $"{Name} ({BluetoothAddress}) [not paired]";
}
