using System.Text.RegularExpressions;

namespace VICE.Lifestyle.Television;

// Retain IDs and link validation so optional YouTube selections survive in saves.
internal static class YouTubeRules
{
    internal static bool IsId(string value) => value.Length == 11 && Regex.IsMatch(value, "^[a-zA-Z0-9_-]{11}$");
    internal static string VideoId(string value)
    {
        if (value.Length > 4096 || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http") || uri.UserInfo.Length != 0 || !uri.IsDefaultPort)
            throw new InvalidDataException("Paste a YouTube video link.");
        var host = uri.Host.ToLowerInvariant();
        string id;
        if (host == "youtu.be") id = uri.AbsolutePath.Trim('/');
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com")
        {
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0] is "shorts" or "embed" or "live") id = parts[1];
            else if (uri.AbsolutePath == "/watch")
            {
                var values = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2))
                    .Where(p => p.Length == 2 && p[0] == "v").ToArray();
                id = values.Length == 1 ? Uri.UnescapeDataString(values[0][1]) : "";
            }
            else id = "";
        }
        else id = "";
        if (!IsId(id)) throw new InvalidDataException("Paste a YouTube watch, short, live or share link for one video.");
        return id;
    }
    internal static void RequireSinglePlayer(bool host, int peers)
    {
        if (!host || peers != 1) throw new InvalidOperationException("YouTube is available in single-player only.");
    }
}
