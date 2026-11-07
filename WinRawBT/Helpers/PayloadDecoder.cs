using System;

namespace WinRawBT.Helpers;

/// <summary>
/// Payload decoder for handling raw bytes and Base64-encoded data (TASK-07)
/// </summary>
public static class PayloadDecoder
{
    /// <summary>
    /// Decode payload from raw bytes or Base64-encoded string (TASK-07)
    /// </summary>
    /// <param name="rawBody">Raw HTTP request body bytes</param>
    /// <param name="contentType">Content-Type header value</param>
    /// <returns>Decoded ESC/POS byte array</returns>
    public static byte[] DecodePayload(byte[] rawBody, string? contentType)
    {
        if (rawBody == null || rawBody.Length == 0)
        {
            throw new ArgumentException("Empty payload");
        }

        contentType = contentType?.ToLowerInvariant().Trim();

        // Handle raw binary (application/octet-stream) - pass through
        if (contentType?.Contains("application/octet-stream") == true)
        {
            return rawBody;
        }

        // Handle JSON or text content - try Base64 decode
        if (contentType?.Contains("text/plain") == true ||
            contentType?.Contains("application/json") == true ||
            contentType?.Contains("text/html") == true ||
            string.IsNullOrEmpty(contentType))
        {
            // Convert to string first
            var textContent = System.Text.Encoding.UTF8.GetString(rawBody).Trim();

            // Remove data URI prefix if present (e.g., "data:application/octet-stream;base64,")
            if (textContent.StartsWith("data:"))
            {
                var base64Start = textContent.IndexOf("base64,");
                if (base64Start >= 0)
                {
                    textContent = textContent.Substring(base64Start + 7);
                }
            }

            // Try to decode as Base64
            if (TryDecodeBase64(textContent, out byte[]? decoded))
            {
                return decoded!;
            }

            // If not valid Base64, treat as raw text (UTF-8 encoded)
            return rawBody;
        }

        // Default: pass through raw bytes
        return rawBody;
    }

    /// <summary>
    /// Try to decode Base64 string with proper validation
    /// </summary>
    private static bool TryDecodeBase64(string text, out byte[]? decoded)
    {
        decoded = null;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Check if it looks like Base64 (only valid characters)
        text = text.Trim();
        
        // Base64 strings should only contain these characters
        foreach (char c in text)
        {
            if (!((c >= 'A' && c <= 'Z') ||
                  (c >= 'a' && c <= 'z') ||
                  (c >= '0' && c <= '9') ||
                  c == '+' || c == '/' || c == '='))
            {
                return false;
            }
        }

        try
        {
            // Try to decode
            decoded = Convert.FromBase64String(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
