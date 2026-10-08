using System.Text.RegularExpressions;

namespace VICE.Lifestyle.Television;

// The companion accepts a video identity, never an extracted media URL.
internal static class YouTubeRules
{
    internal const int Width = 1280, Height = 720, FrameBytes = Width * Height * 4;
    internal const int HeaderBytes = 64, Magic = 1111577686, Protocol = 1;
    internal static bool IsId(string value) => Regex.IsMatch(value, "^[a-zA-Z0-9_-]{11}$");
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
    internal static bool ValidFrame(int magic, int protocol, int width, int height, int stride, int bytes)
        => magic == Magic && protocol == Protocol && width == Width && height == Height && stride == Width * 4 && bytes == FrameBytes;
    internal static void OrientFrame(Span<byte> pixels, Span<byte> row)
    {
        // Chromium starts at the top left; Unity raw textures start at the bottom left.
        const int stride = Width * 4;
        for (var y = 0; y < Height / 2; y++)
        {
            var top = pixels.Slice(y * stride, stride);
            var bottom = pixels.Slice((Height - 1 - y) * stride, stride);
            top.CopyTo(row); bottom.CopyTo(top); row.CopyTo(bottom);
        }
    }
    internal static double Gain(float volume, float personal, float game, float distance)
        => Math.Clamp(volume, 0, 1) * Math.Clamp(personal, 0, 1) * Math.Clamp(game, 0, 1)
            * Math.Clamp((22.0 - distance) / 20.0, 0, 1);
}
