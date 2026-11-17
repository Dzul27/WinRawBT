using System.Globalization;
using System.Text;

namespace WinRawBT.Helpers;

/// <summary>
/// Tiny, dependency-free JSON reader used by <see cref="PayloadDecoder"/> to look inside
/// JSON-wrapped print payloads. Produces <see cref="Dictionary{TKey,TValue}"/> (objects),
/// <see cref="List{T}"/> (arrays), <see cref="string"/>, <see cref="double"/>, <see cref="bool"/> and null.
/// Lenient on purpose: raw tab/CR/LF inside strings are accepted.
/// </summary>
internal static class MiniJson
{
    private const int MaxDepth = 24;

    /// <summary>Parse a complete JSON document. Returns false for anything that is not valid JSON.</summary>
    public static bool TryParse(string text, out object? value)
    {
        value = null;
        int pos = 0;
        try
        {
            SkipWhitespace(text, ref pos);
            object? parsed = ParseValue(text, ref pos, 0);
            SkipWhitespace(text, ref pos);
            if (pos != text.Length)
            {
                return false;
            }

            value = parsed;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static object? ParseValue(string s, ref int pos, int depth)
    {
        if (depth > MaxDepth || pos >= s.Length)
        {
            throw new FormatException();
        }

        char c = s[pos];
        switch (c)
        {
            case '{':
                return ParseObject(s, ref pos, depth);
            case '[':
                return ParseArray(s, ref pos, depth);
            case '"':
                return ParseString(s, ref pos);
            case 't':
                Expect(s, ref pos, "true");
                return true;
            case 'f':
                Expect(s, ref pos, "false");
                return false;
            case 'n':
                Expect(s, ref pos, "null");
                return null;
            default:
                return ParseNumber(s, ref pos);
        }
    }

    private static Dictionary<string, object?> ParseObject(string s, ref int pos, int depth)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        pos++; // {
        SkipWhitespace(s, ref pos);
        if (pos < s.Length && s[pos] == '}')
        {
            pos++;
            return map;
        }

        while (true)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length || s[pos] != '"')
            {
                throw new FormatException();
            }

            string key = ParseString(s, ref pos);
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length || s[pos] != ':')
            {
                throw new FormatException();
            }

            pos++;
            SkipWhitespace(s, ref pos);
            map[key] = ParseValue(s, ref pos, depth + 1);
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length)
            {
                throw new FormatException();
            }

            if (s[pos] == ',')
            {
                pos++;
                continue;
            }

            if (s[pos] == '}')
            {
                pos++;
                return map;
            }

            throw new FormatException();
        }
    }

    private static List<object?> ParseArray(string s, ref int pos, int depth)
    {
        var list = new List<object?>();
        pos++; // [
        SkipWhitespace(s, ref pos);
        if (pos < s.Length && s[pos] == ']')
        {
            pos++;
            return list;
        }

        while (true)
        {
            SkipWhitespace(s, ref pos);
            list.Add(ParseValue(s, ref pos, depth + 1));
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length)
            {
                throw new FormatException();
            }

            if (s[pos] == ',')
            {
                pos++;
                continue;
            }

            if (s[pos] == ']')
            {
                pos++;
                return list;
            }

            throw new FormatException();
        }
    }

    private static string ParseString(string s, ref int pos)
    {
        pos++; // opening quote
        var sb = new StringBuilder();
        while (pos < s.Length)
        {
            char c = s[pos++];
            if (c == '"')
            {
                return sb.ToString();
            }

            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }

            if (pos >= s.Length)
            {
                break;
            }

            char e = s[pos++];
            switch (e)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (pos + 4 > s.Length ||
                        !int.TryParse(s.Substring(pos, 4), NumberStyles.AllowHexSpecifier,
                            CultureInfo.InvariantCulture, out int code))
                    {
                        throw new FormatException();
                    }

                    sb.Append((char)code);
                    pos += 4;
                    break;
                default:
                    throw new FormatException();
            }
        }

        throw new FormatException(); // unterminated string
    }

    private static double ParseNumber(string s, ref int pos)
    {
        int start = pos;
        while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] is '-' or '+' or '.' or 'e' or 'E'))
        {
            pos++;
        }

        if (pos == start ||
            !double.TryParse(s.Substring(start, pos - start), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double number))
        {
            throw new FormatException();
        }

        return number;
    }

    private static void Expect(string s, ref int pos, string literal)
    {
        if (string.CompareOrdinal(s, pos, literal, 0, literal.Length) != 0)
        {
            throw new FormatException();
        }

        pos += literal.Length;
    }

    private static void SkipWhitespace(string s, ref int pos)
    {
        while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t' || s[pos] == '\r' || s[pos] == '\n'))
        {
            pos++;
        }
    }
}
