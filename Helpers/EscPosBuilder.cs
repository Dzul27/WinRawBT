using System.Text;

namespace WinRawBT.Helpers;

/// <summary>
/// Builds ESC/POS command byte arrays (TASK-06).
/// </summary>
public static class EscPosBuilder
{
    // ESC/POS control codes
    private const byte ESC = 0x1B;
    private const byte GS = 0x1D;
    private const byte LF = 0x0A;

    /// <summary>
    /// Build a formatted diagnostic test receipt for the given printer.
    /// Uses CP437-safe ASCII so it renders on virtually all thermal heads.
    /// <paramref name="paperWidthMm"/> 80 uses 48 columns; anything else uses the 58 mm width of 32 columns.
    /// </summary>
    public static byte[] BuildTestReceipt(string printerName, string macAddress, int paperWidthMm = 58)
    {
        using var ms = new MemoryStream();

        // ESC @  -> initialize printer
        ms.WriteByte(ESC);
        ms.WriteByte((byte)'@');

        // ESC a 1 -> center alignment
        ms.WriteByte(ESC);
        ms.WriteByte((byte)'a');
        ms.WriteByte(1);

        // ESC E 1 -> bold on
        ms.WriteByte(ESC);
        ms.WriteByte((byte)'E');
        ms.WriteByte(1);

        WriteLine(ms, "WinRawBT");
        WriteLine(ms, "Bluetooth Print Bridge");

        // ESC E 0 -> bold off
        ms.WriteByte(ESC);
        ms.WriteByte((byte)'E');
        ms.WriteByte(0);

        string rule = new string('-', paperWidthMm == 80 ? 48 : 32);
        WriteLine(ms, rule);
        WriteLine(ms, "TEST CONNECTION");
        WriteLine(ms, "STATUS: CONNECTED");
        WriteLine(ms, rule);

        // ESC a 0 -> left alignment for detail lines
        ms.WriteByte(ESC);
        ms.WriteByte((byte)'a');
        ms.WriteByte(0);

        WriteLine(ms, $"Printer : {Sanitize(printerName)}");
        WriteLine(ms, $"MAC     : {macAddress}");
        WriteLine(ms, $"Time    : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        // 3 line feeds before cut
        ms.WriteByte(LF);
        ms.WriteByte(LF);
        ms.WriteByte(LF);

        // GS V 66 0 -> feed + partial cut (function B)
        ms.WriteByte(GS);
        ms.WriteByte((byte)'V');
        ms.WriteByte(66);
        ms.WriteByte(0);

        return ms.ToArray();
    }

    private static void WriteLine(Stream s, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        s.Write(bytes, 0, bytes.Length);
        s.WriteByte(LF);
    }

    /// <summary>Replace non-ASCII characters so the header/detail lines stay printable.</summary>
    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(unknown)";
        }

        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            sb.Append(c is >= (char)0x20 and < (char)0x7F ? c : '?');
        }

        return sb.ToString();
    }
}
