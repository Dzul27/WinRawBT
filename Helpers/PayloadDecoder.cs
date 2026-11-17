using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WinRawBT.Helpers;

/// <summary>
/// Result of <see cref="PayloadDecoder.Decode"/>: the ESC/POS bytes plus a human-readable
/// description of how they were obtained (shown in the Activity Log).
/// </summary>
public sealed class DecodeResult
{
    internal DecodeResult(byte[] bytes, string path, bool transformed, string? error = null)
    {
        Bytes = bytes;
        Path = path;
        Transformed = transformed;
        Error = error;
    }

    /// <summary>Bytes to send to the printer (empty when <see cref="Error"/> is set or the body was empty).</summary>
    public byte[] Bytes { get; }

    /// <summary>Which decode path was taken, e.g. "raw", "base64 (wrapped lines)", "json "data" -> base64".</summary>
    public string Path { get; }

    /// <summary>False when <see cref="Bytes"/> are the request body unchanged.</summary>
    public bool Transformed { get; }

    /// <summary>Set when the body is recognisably wrapped (JSON, RawBT URL, ...) but unusable; the job is rejected.</summary>
    public string? Error { get; }
}

/// <summary>
/// Decodes incoming print payloads into raw ESC/POS bytes (TASK-07).
/// Accepts raw bytes and the wrappers real POS / RawBT-style clients use. See <see cref="Decode"/>.
/// </summary>
public static class PayloadDecoder
{
    private const byte ESC = 0x1B;
    private const byte GS = 0x1D;
    private const byte FS = 0x1C;
    private const byte DLE = 0x10;
    private const int MaxDepth = 4;

    private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    /// <summary>JSON / form / multipart field names that may carry the payload, in priority order (normalised: lower case, no _ - space).</summary>
    private static readonly string[] DataKeys =
    {
        "data", "payload", "rawdata", "raw", "bytes", "buffer", "base64", "b64", "escpos", "printdata",
        "content", "commands", "command", "cmd", "body", "text", "print", "job", "message", "value",
        "input", "document", "receipt", "output",
    };

    private const RegexOptions Rx = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Regex EnvelopeRegex = new(@"^(?:intent|rawbt):", Rx);
    private static readonly Regex DataUriRegex = new(@"^data:[^\s,]*,", Rx);
    private static readonly Regex EscapedCommandRegex = new(@"\\(?:x1[bd]|u001[bd]|03[35])", Rx);
    private static readonly Regex PartNameRegex = new(@"\bname=""?([^"";\r\n]+)", Rx);
    private static readonly Regex PartFileRegex = new(@"\bfilename=""?([^"";\r\n]*)", Rx);
    private static readonly Regex PartTypeRegex = new(@"^content-type:\s*([^\r\n]+)", Rx | RegexOptions.Multiline);

    /// <summary>
    /// Decode a request body (HTTP POST body or WebSocket message) into ESC/POS bytes.
    /// <list type="bullet">
    /// <item><b>application/octet-stream</b> (and x-binary / escpos types): passed through byte-for-byte.
    /// Exception: a body that starts with a <c>intent:</c>, <c>rawbt:</c> or <c>data:...,</c> envelope is decoded.</item>
    /// <item><b>Anything else</b> (text/plain, application/json, form, none, ...): a body holding control bytes
    /// (ESC/GS) or bytes that are not valid UTF-8 is raw binary and is passed through untouched (never re-encoded,
    /// never trimmed). Otherwise it is text and is checked, in order, for: RawBT/intent URLs
    /// (<c>intent:base64,...#Intent;...;end;</c>, <c>rawbt:base64,...</c>), data URIs, JSON wrappers
    /// (<c>{"data":"..."}</c>, Node Buffer, byte arrays incl. signed -128..127), URL-encoded forms and multipart bodies,
    /// hex strings, Base64 (standard or URL-safe, wrapped lines, missing padding, possibly double-encoded)
    /// and literal <c>\x1b</c>-style escapes. Anything else is plain text and is sent as-is.</item>
    /// <item>A UTF-8 BOM is dropped; UTF-16 text with a BOM is decoded.</item>
    /// <item>Text that is only Base64-alphabet characters is decoded only when the result looks like ESC/POS or
    /// plain ASCII text (or is a long, clean Base64 string), so short plain words are not misread as Base64.</item>
    /// </list>
    /// </summary>
    public static DecodeResult Decode(byte[]? rawBody, string? contentType)
    {
        if (rawBody == null || rawBody.Length == 0)
        {
            return new DecodeResult(Array.Empty<byte>(), "empty body", false);
        }

        return DecodeBody(rawBody, contentType, 0);
    }

    // ---------------------------------------------------------------------
    // Body level
    // ---------------------------------------------------------------------
    private static DecodeResult DecodeBody(byte[] body, string? contentType, int depth)
    {
        ParseContentType(contentType, out string mediaType, out string? charset, out string? boundary);

        if (depth > MaxDepth || (IsBinaryMediaType(mediaType) && !LooksLikeEnvelope(body)))
        {
            return new DecodeResult(body, "raw (binary content type, untouched)", false);
        }

        // Multipart parts are located on the bytes, before any text decoding (file parts hold binary data).
        if (mediaType.StartsWith("multipart/", StringComparison.Ordinal) && boundary != null)
        {
            DecodeResult? multipart = TryMultipart(body, boundary, depth);
            if (multipart != null)
            {
                return multipart;
            }
        }

        byte[] bytes = body;
        string text;
        bool bytesAreText = true; // text is a 1:1 view of bytes (UTF-8 / Latin-1), so bytes can be returned as the fallback
        bool utf8Text = false;

        if (body.Length >= 2 && body[0] == 0xFF && body[1] == 0xFE)
        {
            text = Encoding.Unicode.GetString(body, 2, body.Length - 2);
            bytesAreText = false;
        }
        else if (body.Length >= 2 && body[0] == 0xFE && body[1] == 0xFF)
        {
            text = Encoding.BigEndianUnicode.GetString(body, 2, body.Length - 2);
            bytesAreText = false;
        }
        else
        {
            if (body.Length >= 3 && body[0] == 0xEF && body[1] == 0xBB && body[2] == 0xBF)
            {
                bytes = Slice(body, 3, body.Length - 3);
            }

            if (charset != null && (charset.StartsWith("utf-16", StringComparison.Ordinal) || charset == "unicode"))
            {
                text = (charset.EndsWith("be", StringComparison.Ordinal) ? Encoding.BigEndianUnicode : Encoding.Unicode)
                    .GetString(bytes);
                bytesAreText = false;
            }
            else if (charset == null || charset == "utf-8" || charset == "utf8")
            {
                try
                {
                    text = StrictUtf8.GetString(bytes);
                    utf8Text = true;
                }
                catch (DecoderFallbackException)
                {
                    return new DecodeResult(bytes, "raw (body is not valid UTF-8, untouched)", bytes.Length != body.Length);
                }
            }
            else
            {
                text = Latin1.GetString(bytes);
            }
        }

        if (HasControlChars(text))
        {
            if (!bytesAreText)
            {
                return new DecodeResult(StringToBytes(text), "UTF-16 text holding control bytes", true);
            }

            if (utf8Text && IsLatin1BinaryString(text))
            {
                return new DecodeResult(Latin1.GetBytes(text), "raw (text frame carried a binary string; chars restored to single bytes)", true);
            }

            return new DecodeResult(bytes, "raw (body holds ESC/POS control bytes, untouched)", bytes.Length != body.Length);
        }

        bool form = mediaType == "application/x-www-form-urlencoded";
        return DecodeText(text, bytesAreText ? bytes : null, depth, form, explicitBase64: false, strict: false);
    }

    private static void ParseContentType(string? contentType, out string mediaType, out string? charset, out string? boundary)
    {
        mediaType = string.Empty;
        charset = null;
        boundary = null;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return;
        }

        string[] parts = contentType!.Split(';');
        mediaType = parts[0].Trim().ToLowerInvariant();
        for (int i = 1; i < parts.Length; i++)
        {
            int eq = parts[i].IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            string name = parts[i].Substring(0, eq).Trim().ToLowerInvariant();
            string value = parts[i].Substring(eq + 1).Trim().Trim('"');
            if (name == "charset")
            {
                charset = value.ToLowerInvariant();
            }
            else if (name == "boundary")
            {
                boundary = value;
            }
        }
    }

    private static bool IsBinaryMediaType(string mediaType) =>
        mediaType == "application/octet-stream" || mediaType == "application/x-binary" ||
        mediaType == "application/binary" || mediaType == "application/x-escpos" ||
        mediaType == "application/escpos" || mediaType == "application/vnd.escpos";

    private static bool LooksLikeEnvelope(byte[] body)
    {
        string head = Latin1.GetString(body, 0, Math.Min(body.Length, 256)).TrimStart();
        return EnvelopeRegex.IsMatch(head) || DataUriRegex.IsMatch(head);
    }

    /// <summary>True when the text holds control characters other than TAB/CR/LF (i.e. ESC/POS binary, not text).</summary>
    private static bool HasControlChars(string text)
    {
        foreach (char c in text)
        {
            if (c < 0x20 && c != '\t' && c != '\n' && c != '\r')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A "binary string" (JS <c>String.fromCharCode</c> bytes) sent as UTF-8 text: contains ESC/GS and
    /// bytes &gt;= 0x80, with every char &lt;= U+00FF. Only then is it safe to restore single bytes.
    /// </summary>
    private static bool IsLatin1BinaryString(string text)
    {
        bool high = false;
        bool command = false;
        foreach (char c in text)
        {
            if (c > 0xFF)
            {
                return false;
            }

            if (c >= 0x80)
            {
                high = true;
            }
            else if (c == ESC || c == GS)
            {
                command = true;
            }
        }

        return high && command;
    }

    private static byte[] StringToBytes(string text) =>
        IsLatin1BinaryString(text) ? Latin1.GetBytes(text) : Encoding.UTF8.GetBytes(text);

    // ---------------------------------------------------------------------
    // Text level
    // ---------------------------------------------------------------------
    /// <param name="fallback">Bytes to return when the text is plain (original body bytes); null = encode the text.</param>
    /// <param name="explicitBase64">The text is known to be Base64 (JSON key "base64"/"b64"): skip the plausibility check.</param>
    /// <param name="strict">Nested decode of an already decoded payload: only accept results that start with ESC/GS.</param>
    private static DecodeResult DecodeText(string text, byte[]? fallback, int depth, bool formHint, bool explicitBase64, bool strict)
    {
        string t = text.Trim();
        if (t.Length == 0)
        {
            return new DecodeResult(Array.Empty<byte>(), "empty (whitespace only)", false);
        }

        if (depth > MaxDepth)
        {
            return Plain(text, fallback, null);
        }

        DecodeResult? envelope = TryEnvelope(t, depth);
        if (envelope != null)
        {
            return envelope;
        }

        bool looksLikeJson = t[0] == '{' || t[0] == '[' || t[0] == '"';
        if (looksLikeJson && MiniJson.TryParse(t, out object? json))
        {
            return ExtractJson(json, depth, "json", false);
        }

        if (!strict && (formHint || StartsWithDataKey(t)))
        {
            DecodeResult? form = TryForm(t, depth);
            if (form != null)
            {
                return form;
            }
        }

        if (TryHex(t, out byte[] hex))
        {
            return new DecodeResult(hex, "hex text", true);
        }

        if (TryBase64(t, explicitBase64, strict, out byte[] b64, out string variant))
        {
            return FinishBase64(b64, "base64 (" + variant + ")", depth);
        }

        if (EscapedCommandRegex.IsMatch(t))
        {
            return new DecodeResult(UnescapeText(text), "escaped text (\\x1b-style sequences converted)", true);
        }

        return Plain(text, fallback, looksLikeJson && (t[0] == '{' || t[0] == '[')
            ? "text as-is (starts like JSON but is not valid JSON)"
            : null);
    }

    private static DecodeResult Plain(string text, byte[]? fallback, string? note)
    {
        if (fallback != null)
        {
            return new DecodeResult(fallback, note ?? "text as-is (no wrapper detected)", false);
        }

        bool restored = IsLatin1BinaryString(text);
        return new DecodeResult(restored ? Latin1.GetBytes(text) : Encoding.UTF8.GetBytes(text),
            (note ?? "text as-is (no wrapper detected)") + (restored ? ", binary string restored to single bytes" : string.Empty), false);
    }

    private static DecodeResult Fail(string message) =>
        new DecodeResult(Array.Empty<byte>(), message, false, message);

    private static DecodeResult Prefix(string trail, DecodeResult r) =>
        new DecodeResult(r.Bytes, trail + " -> " + r.Path, true, r.Error);

    /// <summary>RawBT / Android intent URLs and data URIs.</summary>
    private static DecodeResult? TryEnvelope(string t, int depth)
    {
        Match m = EnvelopeRegex.Match(t);
        if (m.Success)
        {
            string label = m.Value.TrimEnd(':').ToLowerInvariant();
            string rest = t.Substring(m.Length);
            int tail = rest.IndexOf("#Intent;", StringComparison.OrdinalIgnoreCase);
            if (tail >= 0)
            {
                rest = rest.Substring(0, tail);
            }

            if (rest.StartsWith("//", StringComparison.Ordinal))
            {
                rest = rest.Substring(2);
            }

            if (rest.StartsWith("base64,", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryBase64(rest.Substring(7), true, false, out byte[] b64, out string variant))
                {
                    return Fail(label + ":base64 envelope does not contain valid Base64");
                }

                return FinishBase64(b64, label + ":base64 envelope (" + variant + ")", depth);
            }

            return new DecodeResult(PercentDecode(rest, false), label + ": envelope with percent-encoded text", true);
        }

        if (DataUriRegex.IsMatch(t))
        {
            int comma = t.IndexOf(',');
            string header = t.Substring(5, comma - 5);
            string data = t.Substring(comma + 1);
            if (header.IndexOf(";base64", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!TryBase64(data, true, false, out byte[] b64, out string variant))
                {
                    return Fail("data-URI does not contain valid Base64");
                }

                return FinishBase64(b64, "data-URI base64 (" + variant + ")", depth);
            }

            return new DecodeResult(PercentDecode(data, false), "data-URI with percent-encoded text", true);
        }

        return null;
    }

    // ---------------------------------------------------------------------
    // JSON
    // ---------------------------------------------------------------------
    private static DecodeResult ExtractJson(object? node, int depth, string trail, bool explicitBase64)
    {
        if (depth > MaxDepth)
        {
            return Fail("JSON wrapper is nested too deeply");
        }

        switch (node)
        {
            case string str:
                return Prefix(trail, DecodeText(str, null, depth + 1, false, explicitBase64, false));

            case List<object?> list:
                return TryByteArray(list, out byte[] numbers)
                    ? new DecodeResult(numbers, trail + " byte array", true)
                    : Fail(trail + " is an array that is not a list of byte values (0-255 or -128..127)");

            case Dictionary<string, object?> map:
                DecodeResult? lastFailure = null;
                foreach (string key in DataKeys)
                {
                    foreach (KeyValuePair<string, object?> entry in map)
                    {
                        if (entry.Value == null || NormalizeKey(entry.Key) != key)
                        {
                            continue;
                        }

                        DecodeResult r = ExtractJson(entry.Value, depth + 1,
                            trail + " \"" + entry.Key + "\"", key == "base64" || key == "b64");
                        if (r.Error == null)
                        {
                            return r;
                        }

                        lastFailure = r;
                    }
                }

                if (lastFailure != null)
                {
                    return lastFailure;
                }

                if (map.Count == 1)
                {
                    KeyValuePair<string, object?> only = map.First();
                    return ExtractJson(only.Value, depth + 1, trail + " \"" + only.Key + "\"", false);
                }

                return Fail("JSON object has no recognised payload field (keys: " +
                            string.Join(", ", map.Keys.Take(12)) + "). Expected one of: data, payload, bytes, base64, escpos, content, commands, text ...");

            default:
                return Fail(trail + " holds a number/boolean/null instead of print data");
        }
    }

    private static bool TryByteArray(List<object?> list, out byte[] bytes)
    {
        bytes = new byte[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is not double d || d != Math.Floor(d) || d < -128 || d > 255)
            {
                return false;
            }

            bytes[i] = (byte)(d < 0 ? d + 256 : d); // signed Java/Kotlin bytes -> unsigned
        }

        return true;
    }

    // ---------------------------------------------------------------------
    // Form / multipart
    // ---------------------------------------------------------------------
    private static bool StartsWithDataKey(string t)
    {
        int eq = t.IndexOf('=');
        if (eq <= 0 || eq > 24)
        {
            return false;
        }

        for (int i = 0; i < eq; i++)
        {
            char c = t[i];
            if (!(char.IsLetterOrDigit(c) && c < 0x80) && c != '_' && c != '-')
            {
                return false;
            }
        }

        return Array.IndexOf(DataKeys, NormalizeKey(t.Substring(0, eq))) >= 0;
    }

    private static DecodeResult? TryForm(string t, int depth)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string pair in t.Split('&'))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            string key = NormalizeKey(Latin1.GetString(PercentDecode(pair.Substring(0, eq), true)));
            if (!fields.ContainsKey(key))
            {
                fields[key] = pair.Substring(eq + 1);
            }
        }

        foreach (string key in DataKeys)
        {
            if (!fields.TryGetValue(key, out string? raw))
            {
                continue;
            }

            // Keep '+' literal first: an unescaped Base64 '+' must not become a space.
            byte[] v1 = PercentDecode(raw, false);
            DecodeResult r = DecodeText(BytesToText(v1), v1, depth + 1, false, key == "base64" || key == "b64", false);
            if (!r.Transformed && r.Error == null && raw.IndexOf('+') >= 0)
            {
                byte[] v2 = PercentDecode(raw, true);
                r = DecodeText(BytesToText(v2), v2, depth + 1, false, false, false);
            }

            return Prefix("form field \"" + key + "\"", r);
        }

        return null;
    }

    private static DecodeResult? TryMultipart(byte[] body, string boundary, int depth)
    {
        byte[] delimiter = Latin1.GetBytes("--" + boundary);
        byte[] partEnd = Latin1.GetBytes("\r\n--" + boundary);
        byte[] headerEnd = { 13, 10, 13, 10 };

        var parts = new List<(string Name, string? FileName, string? Type, byte[] Content)>();
        int pos = IndexOf(body, delimiter, 0);
        while (pos >= 0)
        {
            pos += delimiter.Length;
            if (pos + 1 < body.Length && body[pos] == '-' && body[pos + 1] == '-')
            {
                break;
            }

            if (pos + 1 < body.Length && body[pos] == 13 && body[pos + 1] == 10)
            {
                pos += 2;
            }

            int headersEnd = IndexOf(body, headerEnd, pos);
            if (headersEnd < 0)
            {
                break;
            }

            string headers = Latin1.GetString(body, pos, headersEnd - pos);
            int contentStart = headersEnd + 4;
            int next = IndexOf(body, partEnd, contentStart);
            if (next < 0)
            {
                break;
            }

            Match name = PartNameRegex.Match(headers);
            Match file = PartFileRegex.Match(headers);
            Match type = PartTypeRegex.Match(headers);
            parts.Add((
                name.Success ? name.Groups[1].Value : string.Empty,
                file.Success ? file.Groups[1].Value : null,
                type.Success ? type.Groups[1].Value.Trim() : null,
                Slice(body, contentStart, next - contentStart)));
            pos = next + 2;
        }

        if (parts.Count == 0)
        {
            return null;
        }

        int chosen = -1;
        foreach (string key in DataKeys)
        {
            chosen = parts.FindIndex(p => NormalizeKey(p.Name) == key);
            if (chosen >= 0)
            {
                break;
            }
        }

        if (chosen < 0)
        {
            chosen = parts.FindIndex(p => p.FileName != null);
        }

        if (chosen < 0 && parts.Count == 1)
        {
            chosen = 0;
        }

        if (chosen < 0)
        {
            return Fail("multipart body has no recognised data field or file (fields: " +
                        string.Join(", ", parts.Select(p => p.Name).Take(12)) + ")");
        }

        var part = parts[chosen];
        DecodeResult r = DecodeBody(part.Content, part.Type, depth + 1);
        return Prefix("multipart part \"" + part.Name + "\"", r);
    }

    // ---------------------------------------------------------------------
    // Hex
    // ---------------------------------------------------------------------
    /// <summary>"1b 40 41" / "1B4041" / "0x1B,0x40" / "\x1b\x40". Accepted only when the first byte is an ESC/POS command byte.</summary>
    private static bool TryHex(string t, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        var digits = new StringBuilder(t.Length);
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            if (Uri.IsHexDigit(c))
            {
                if (c == '0' && i + 1 < t.Length && (t[i + 1] == 'x' || t[i + 1] == 'X'))
                {
                    i++; // 0x prefix
                    continue;
                }

                digits.Append(c);
            }
            else if (c == '\\' && i + 1 < t.Length && t[i + 1] == 'x')
            {
                i++; // \x prefix
            }
            else if (c is ' ' or '\t' or '\r' or '\n' or ',' or ':' or ';')
            {
                // separator
            }
            else
            {
                return false;
            }
        }

        if (digits.Length < 6 || digits.Length % 2 != 0)
        {
            return false;
        }

        var result = new byte[digits.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = byte.Parse(digits.ToString(i * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        }

        if (result[0] != ESC && result[0] != GS)
        {
            return false;
        }

        bytes = result;
        return true;
    }

    // ---------------------------------------------------------------------
    // Base64
    // ---------------------------------------------------------------------
    /// <summary>
    /// Tolerant Base64: standard or URL-safe alphabet, whitespace / wrapped lines, missing padding, percent-encoded
    /// <c>+ / =</c>. Unless <paramref name="explicitBase64"/>, plain-looking strings are only accepted when the
    /// decoded bytes look like ESC/POS or ASCII text (or the string is long and clean).
    /// </summary>
    private static bool TryBase64(string text, bool explicitBase64, bool strict, out byte[] bytes, out string variant)
    {
        bytes = Array.Empty<byte>();
        variant = string.Empty;

        string s = text.Trim();
        if (s.Length < (explicitBase64 ? 2 : 4))
        {
            return false;
        }

        bool percent = false;
        if (s.IndexOf('%') >= 0)
        {
            s = Latin1.GetString(PercentDecode(s, false));
            percent = true;
        }

        string[] tokens = s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        bool standardChars = false;
        bool urlChars = false;
        foreach (string token in tokens)
        {
            foreach (char c in token)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '=')
                {
                    continue;
                }

                if (c == '+' || c == '/')
                {
                    standardChars = true;
                }
                else if (c == '-' || c == '_')
                {
                    urlChars = true;
                }
                else
                {
                    return false;
                }
            }
        }

        if (standardChars && urlChars)
        {
            return false;
        }

        bool wrapped = tokens.Length > 1;
        if (wrapped && !explicitBase64)
        {
            // Wrapped Base64 (MIME/PEM): equal-length lines (multiple of 4, >= 16), shorter last line.
            int lineLength = tokens[0].Length;
            if (lineLength < 16 || lineLength % 4 != 0)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length - 1; i++)
            {
                if (tokens[i].Length != lineLength)
                {
                    return false;
                }
            }

            if (tokens[tokens.Length - 1].Length > lineLength)
            {
                return false;
            }
        }

        string joined = string.Concat(tokens);
        string core = joined.TrimEnd('=');
        if (core.Length == 0 || core.IndexOf('=') >= 0 || joined.Length - core.Length > 2)
        {
            return false;
        }

        int remainder = core.Length % 4;
        if (remainder == 1)
        {
            return false;
        }

        string normalized = urlChars ? core.Replace('-', '+').Replace('_', '/') : core;
        string padded = normalized + new string('=', (4 - remainder) % 4);
        bool paddingFixed = padded.Length != joined.Length;

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            return false;
        }

        if (decoded.Length == 0)
        {
            return false;
        }

        if (!explicitBase64)
        {
            bool accept;
            if (strict)
            {
                accept = StartsWithCommand(decoded);
            }
            else
            {
                bool clean = !wrapped && !urlChars && !paddingFixed && !percent;
                accept = StartsWithCommand(decoded) || wrapped ||
                         (clean && (joined.Length >= 24 || (joined.Length >= 8 && IsPlainAscii(decoded))));
            }

            if (!accept)
            {
                return false;
            }
        }

        var notes = new List<string>();
        if (wrapped) { notes.Add("wrapped lines"); }
        if (urlChars) { notes.Add("URL-safe"); }
        if (paddingFixed) { notes.Add("padding restored"); }
        if (percent) { notes.Add("percent-decoded"); }
        variant = notes.Count == 0 ? "standard" : string.Join(", ", notes);
        bytes = decoded;
        return true;
    }

    /// <summary>Base64 decoded; if the result is itself an encoded payload (double Base64, Base64 JSON...) unwrap once more.</summary>
    private static DecodeResult FinishBase64(byte[] bytes, string label, int depth)
    {
        if (depth < MaxDepth && IsPlainAscii(bytes) && bytes.Length >= 4)
        {
            DecodeResult nested = DecodeText(Latin1.GetString(bytes), null, depth + 1, false, false, true);
            if (nested.Transformed && nested.Error == null && nested.Bytes.Length > 0)
            {
                return new DecodeResult(nested.Bytes, label + " -> " + nested.Path, true);
            }
        }

        return new DecodeResult(bytes, label, true);
    }

    private static bool StartsWithCommand(byte[] b) =>
        b.Length > 0 && (b[0] == ESC || b[0] == GS || b[0] == FS || b[0] == DLE);

    private static bool IsPlainAscii(byte[] b)
    {
        if (b.Length == 0)
        {
            return false;
        }

        foreach (byte x in b)
        {
            if (!((x >= 0x20 && x <= 0x7E) || x == 9 || x == 10 || x == 13))
            {
                return false;
            }
        }

        return true;
    }

    // ---------------------------------------------------------------------
    // Escapes / helpers
    // ---------------------------------------------------------------------
    /// <summary>Convert literal <c>\x1b \u001b \033 \e \n \r \t \\</c> sequences to bytes.</summary>
    private static byte[] UnescapeText(string s)
    {
        using var ms = new MemoryStream(s.Length);
        var run = new StringBuilder();

        void FlushRun()
        {
            if (run.Length > 0)
            {
                byte[] encoded = Encoding.UTF8.GetBytes(run.ToString());
                ms.Write(encoded, 0, encoded.Length);
                run.Clear();
            }
        }

        void Emit(byte value)
        {
            FlushRun();
            ms.WriteByte(value);
        }

        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c != '\\' || i + 1 >= s.Length)
            {
                run.Append(c);
                i++;
                continue;
            }

            char n = s[i + 1];
            if ((n == 'x' || n == 'X') && IsHex(s, i + 2, 2))
            {
                Emit(Convert.ToByte(s.Substring(i + 2, 2), 16));
                i += 4;
            }
            else if (n == 'u' && IsHex(s, i + 2, 4))
            {
                int code = Convert.ToInt32(s.Substring(i + 2, 4), 16);
                if (code <= 0xFF)
                {
                    Emit((byte)code);
                }
                else
                {
                    run.Append((char)code);
                }

                i += 6;
            }
            else if (n >= '0' && n <= '7')
            {
                int j = i + 1;
                int value = 0;
                int count = 0;
                while (j < s.Length && count < 3 && s[j] >= '0' && s[j] <= '7')
                {
                    value = value * 8 + (s[j] - '0');
                    j++;
                    count++;
                }

                Emit((byte)(value & 0xFF));
                i = j;
            }
            else
            {
                switch (n)
                {
                    case 'n': Emit(0x0A); i += 2; break;
                    case 'r': Emit(0x0D); i += 2; break;
                    case 't': Emit(0x09); i += 2; break;
                    case 'a': Emit(0x07); i += 2; break;
                    case 'b': Emit(0x08); i += 2; break;
                    case 'f': Emit(0x0C); i += 2; break;
                    case 'v': Emit(0x0B); i += 2; break;
                    case 'e': Emit(ESC); i += 2; break;
                    case '\\': run.Append('\\'); i += 2; break;
                    default: run.Append(c); i++; break;
                }
            }
        }

        FlushRun();
        return ms.ToArray();
    }

    private static bool IsHex(string s, int start, int count)
    {
        if (start + count > s.Length)
        {
            return false;
        }

        for (int i = start; i < start + count; i++)
        {
            if (!Uri.IsHexDigit(s[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Byte-exact percent decoding (%XX -> byte). <c>+</c> becomes a space only when asked (form encoding).</summary>
    private static byte[] PercentDecode(string s, bool plusIsSpace)
    {
        using var ms = new MemoryStream(s.Length);
        var run = new StringBuilder();

        void FlushRun()
        {
            if (run.Length > 0)
            {
                byte[] encoded = Encoding.UTF8.GetBytes(run.ToString());
                ms.Write(encoded, 0, encoded.Length);
                run.Clear();
            }
        }

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '%' && IsHex(s, i + 1, 2))
            {
                FlushRun();
                ms.WriteByte(Convert.ToByte(s.Substring(i + 1, 2), 16));
                i += 2;
            }
            else if (c == '+' && plusIsSpace)
            {
                FlushRun();
                ms.WriteByte(0x20);
            }
            else
            {
                run.Append(c);
            }
        }

        FlushRun();
        return ms.ToArray();
    }

    private static string BytesToText(byte[] bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Latin1.GetString(bytes);
        }
    }

    private static string NormalizeKey(string key) =>
        key.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);

    private static byte[] Slice(byte[] source, int start, int count)
    {
        var result = new byte[count];
        Buffer.BlockCopy(source, start, result, 0, count);
        return result;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (int i = start; i <= haystack.Length - needle.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j])
            {
                j++;
            }

            if (j == needle.Length)
            {
                return i;
            }
        }

        return -1;
    }

    // ---------------------------------------------------------------------
    // Diagnostics
    // ---------------------------------------------------------------------
    /// <summary>
    /// Single-line hex + ASCII preview of the first <paramref name="maxBytes"/> bytes, e.g.
    /// <c>1B 40 54 45 53 54 | .@TEST</c>; longer payloads append the last 8 bytes.
    /// </summary>
    public static string FormatPreview(byte[] data, int maxBytes = 48)
    {
        int n = Math.Min(data.Length, maxBytes);
        var sb = new StringBuilder(n * 4 + 40);
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        sb.Append(" | ");
        for (int i = 0; i < n; i++)
        {
            sb.Append(data[i] >= 0x20 && data[i] <= 0x7E ? (char)data[i] : '.');
        }

        if (data.Length > n)
        {
            sb.Append(" ... tail: ");
            int tailStart = Math.Max(n, data.Length - 8);
            for (int i = tailStart; i < data.Length; i++)
            {
                if (i > tailStart)
                {
                    sb.Append(' ');
                }

                sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return sb.ToString();
    }

    /// <summary>Quoted ASCII head of the request body as received (non-printables shown as '.').</summary>
    public static string FormatBodyHead(byte[] body, int maxChars = 64)
    {
        int n = Math.Min(body.Length, maxChars);
        var sb = new StringBuilder(n + 8);
        sb.Append('"');
        for (int i = 0; i < n; i++)
        {
            sb.Append(body[i] >= 0x20 && body[i] <= 0x7E ? (char)body[i] : '.');
        }

        sb.Append('"');
        if (body.Length > n)
        {
            sb.Append("...");
        }

        return sb.ToString();
    }

    /// <summary>True when the payload already begins with the ESC @ (initialize printer) command.</summary>
    public static bool StartsWithInit(byte[] data) =>
        data.Length >= 2 && data[0] == ESC && data[1] == (byte)'@';

    /// <summary>One-line ESC/POS feature scan (code page, raster images, QR, cut...) to help diagnose garbled output.</summary>
    public static string SummarizeEscPos(byte[] d)
    {
        var codePages = new List<int>();
        int raster = 0, twoD = 0, cut = 0, drawer = 0, high = 0, lineFeeds = 0;
        for (int i = 0; i < d.Length; i++)
        {
            byte b = d[i];
            if (b == GS && i + 7 < d.Length && d[i + 1] == (byte)'v' && d[i + 2] == (byte)'0')
            {
                int w = d[i + 4] | (d[i + 5] << 8);
                int h = d[i + 6] | (d[i + 7] << 8);
                raster++;
                i = (int)Math.Min((long)d.Length - 1, i + 7L + (long)w * h);
                continue;
            }

            if (b >= 0x80)
            {
                high++;
            }
            else if (b == 0x0A)
            {
                lineFeeds++;
            }
            else if (b == ESC && i + 2 < d.Length && d[i + 1] == (byte)'t')
            {
                if (!codePages.Contains(d[i + 2]) && codePages.Count < 6)
                {
                    codePages.Add(d[i + 2]);
                }
            }
            else if (b == ESC && i + 1 < d.Length && d[i + 1] == (byte)'p')
            {
                drawer++;
            }
            else if (b == GS && i + 2 < d.Length && d[i + 1] == (byte)'(' && d[i + 2] == (byte)'k')
            {
                twoD++;
            }
            else if (b == GS && i + 1 < d.Length && d[i + 1] == (byte)'V')
            {
                cut++;
            }
        }

        return "ESC/POS scan: starts with ESC @=" + (StartsWithInit(d) ? "yes" : "NO") +
               ", code page (ESC t n)=" + (codePages.Count == 0 ? "none set" : string.Join("/", codePages)) +
               ", raster images (GS v 0)=" + raster + ", QR/2D (GS ( k)=" + twoD +
               ", cut (GS V)=" + cut + ", drawer (ESC p)=" + drawer +
               ", line feeds=" + lineFeeds + ", bytes>=0x80 outside images=" + high;
    }
}
