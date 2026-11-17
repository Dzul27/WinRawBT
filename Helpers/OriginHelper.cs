namespace WinRawBT.Helpers;

/// <summary>Turns what a user types ("pos.example.com", "https://pos.example.com/app") into a browser Origin.</summary>
public static class OriginHelper
{
    /// <summary>
    /// Returns <c>scheme://host[:port]</c> in lower case (default ports dropped, like the browser's Origin header),
    /// <c>"null"</c> for the file:// origin, or null when the input is not a usable http(s) address.
    /// A missing scheme defaults to https.
    /// </summary>
    public static string? Normalize(string? input)
    {
        string text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        if (text.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return "null";
        }

        if (!text.Contains("://"))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    }
}
