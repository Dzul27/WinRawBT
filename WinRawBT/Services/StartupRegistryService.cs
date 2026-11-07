using System;
using Microsoft.Win32;

namespace WinRawBT.Services;

/// <summary>
/// Windows Startup Registry service (TASK-15)
/// Manages application startup behavior via Windows registry
/// </summary>
public static class StartupRegistryService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppKeyName = "WinRawBT";

    /// <summary>
    /// Set application to run on Windows startup (TASK-15)
    /// </summary>
    /// <param name="enable">True to enable startup, false to disable</param>
    public static void SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            
            if (key == null)
            {
                throw new InvalidOperationException("Unable to open registry key");
            }

            if (enable)
            {
                // Add to startup
                var exePath = Application.ExecutablePath;
                key.SetValue(AppKeyName, exePath);
            }
            else
            {
                // Remove from startup
                key.DeleteValue(AppKeyName, false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting startup: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Check if application is set to run on startup
    /// </summary>
    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            
            if (key == null)
                return false;

            var value = key.GetValue(AppKeyName);
            return value != null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking startup: {ex.Message}");
            return false;
        }
    }
}
