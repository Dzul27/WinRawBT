using System;
using System.Text;

namespace WinRawBT.Helpers;

/// <summary>
/// ESC/POS command builder for thermal printers (TASK-06)
/// </summary>
public static class EscPosBuilder
{
    // ESC/POS Commands
    private static readonly byte ESC = 0x1B;  // Escape
    private static readonly byte GS = 0x1D;   // Group Separator
    
    /// <summary>
    /// Build test receipt for printer diagnostics (TASK-06)
    /// </summary>
    public static byte[] BuildTestReceipt(string printerName, string macAddress)
    {
        using var ms = new System.IO.MemoryStream();
        
        // Initialize printer
        WriteCommand(ms, ESC, (byte)'@');
        
        // Center align
        WriteCommand(ms, ESC, (byte)'a', 1);
        
        // Enable bold
        WriteCommand(ms, ESC, (byte)'E', 1);
        
        // Print header
        WriteText(ms, "=== WinRawBT Test ===\n");
        
        // Disable bold
        WriteCommand(ms, ESC, (byte)'E', 0);
        
        // Print connection status
        WriteText(ms, "STATUS: CONNECTED\n\n");
        
        // Left align for details
        WriteCommand(ms, ESC, (byte)'a', 0);
        
        // Print printer details
        WriteText(ms, $"Printer: {printerName}\n");
        WriteText(ms, $"MAC: {macAddress}\n");
        WriteText(ms, $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n");
        
        // Print separator
        WriteText(ms, "------------------------\n");
        WriteText(ms, "Connection Test: SUCCESS\n");
        WriteText(ms, "------------------------\n\n");
        
        // Feed 3 lines
        WriteCommand(ms, ESC, (byte)'d', 3);
        
        // Cut paper (GS V 66 0) - Full cut
        WriteCommand(ms, GS, (byte)'V', 66, 0);
        
        return ms.ToArray();
    }

    /// <summary>
    /// Initialize printer
    /// </summary>
    public static byte[] Initialize()
    {
        using var ms = new System.IO.MemoryStream();
        WriteCommand(ms, ESC, (byte)'@');
        return ms.ToArray();
    }

    /// <summary>
    /// Center align text
    /// </summary>
    public static byte[] CenterAlign()
    {
        using var ms = new System.IO.MemoryStream();
        WriteCommand(ms, ESC, (byte)'a', 1);
        return ms.ToArray();
    }

    /// <summary>
    /// Left align text
    /// </summary>
    public static byte[] LeftAlign()
    {
        using var ms = new System.IO.MemoryStream();
        WriteCommand(ms, ESC, (byte)'a', 0);
        return ms.ToArray();
    }

    /// <summary>
    /// Feed and cut paper
    /// </summary>
    public static byte[] FeedAndCut(int lines = 3)
    {
        using var ms = new System.IO.MemoryStream();
        WriteCommand(ms, ESC, (byte)'d', (byte)lines);
        WriteCommand(ms, GS, (byte)'V', 66, 0);
        return ms.ToArray();
    }

    /// <summary>
    /// Write text to stream
    /// </summary>
    private static void WriteText(System.IO.MemoryStream ms, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        ms.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Write ESC/POS command to stream
    /// </summary>
    private static void WriteCommand(System.IO.MemoryStream ms, params byte[] command)
    {
        ms.Write(command, 0, command.Length);
    }
}
