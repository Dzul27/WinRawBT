using Microsoft.Win32;

namespace WinRawBT.Services;

/// <summary>
/// Toggles the Windows startup registry entry under HKCU (TASK-15).
/// Writing to HKCU\...\Run does not require admin elevation (NFR-3).
/// </summary>
public static class StartupRegistryService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WinRawBT";

    /// <summary>Add or remove the startup Run entry.</summary>
    public static bool SetStartup(bool enable)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null)
            {
                return false;
            }

            if (enable)
            {
                string exePath = GetExecutablePath();
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>True if the Run entry currently exists.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    private static string GetExecutablePath()
    {
        return Application.ExecutablePath;
    }
}
