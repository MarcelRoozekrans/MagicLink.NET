namespace MagicLink;

/// <summary>Checks that an address to return to after signing in stays on this site.</summary>
internal static class LocalUrl
{
    /// <summary>
    /// True for a path on this site only. Browsers read <c>//host</c> and <c>/\host</c> as another site, so a second
    /// character of <c>/</c> or <c>\</c> is refused, as are control characters, which browsers strip before reading
    /// the address, so that <c>/&lt;tab&gt;/host</c> becomes <c>//host</c>.
    /// </summary>
    public static bool IsLocal(string? url)
    {
        if (string.IsNullOrEmpty(url) || url[0] != '/')
        {
            return false;
        }

        if (url.Length > 1 && url[1] is '/' or '\\')
        {
            return false;
        }

        foreach (var character in url)
        {
            if (char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }
}
