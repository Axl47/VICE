using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VICE.Lifestyle.Television;

namespace VICE.YouTube;

internal sealed record YouTubeChapterResult(IReadOnlyList<YouTubeChapter> Chapters, string Status);

// The companion has no chapter command. Read only the selected watch page's player
// metadata; never execute page scripts or infer chapters from arbitrary timestamps.
internal static class YouTubeChapters
{
    internal const int MaxPageBytes = 4 * 1024 * 1024, MaxChapters = 256;
    internal static async Task<YouTubeChapterResult> Fetch(string videoId, CancellationToken cancellation)
    {
        try
        {
            if (!TVYouTube.IsVideoId(videoId)) throw new InvalidDataException("Invalid video identity.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            cancellation = deadline.Token;
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
            using var response = await client.GetAsync("https://www.youtube.com/watch?v=" + videoId + "&hl=en", HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxPageBytes) throw new InvalidDataException("Watch page too large.");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
            using var page = new MemoryStream();
            var buffer = new byte[16384];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0)
            {
                if (page.Length + read > MaxPageBytes) throw new InvalidDataException("Watch page too large.");
                page.Write(buffer, 0, read);
            }
            cancellation.ThrowIfCancellationRequested();
            var chapters = ParsePage(Encoding.UTF8.GetString(page.GetBuffer(), 0, (int)page.Length), videoId);
            return new(chapters, chapters.Count > 0 ? "" : "This video has no available chapters.");
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException or ArgumentException or InvalidOperationException or RegexMatchTimeoutException)
        { return new(Array.Empty<YouTubeChapter>(), "Chapters unavailable. Playback still works; Retry reloads chapters."); }
    }

    internal static IReadOnlyList<YouTubeChapter> ParsePage(string html, string videoId)
    {
        if (!TVYouTube.IsVideoId(videoId)) throw new InvalidDataException("Invalid video identity.");
        if (Encoding.UTF8.GetByteCount(html) > MaxPageBytes) throw new InvalidDataException("Watch page too large.");
        using var player = JsonDocument.Parse(ObjectAssignment(html, "ytInitialPlayerResponse"), new() { MaxDepth = 128 });
        if (!player.RootElement.TryGetProperty("videoDetails", out var details) || !details.TryGetProperty("videoId", out var identity)
            || identity.ValueKind != JsonValueKind.String || identity.GetString() != videoId)
            throw new InvalidDataException("Watch page video identity mismatch.");
        using var data = JsonDocument.Parse(ObjectAssignment(html, "ytInitialData"), new() { MaxDepth = 128 });
        var bar = data.RootElement;
        foreach (var key in new[] { "playerOverlays", "playerOverlayRenderer", "decoratedPlayerBarRenderer", "decoratedPlayerBarRenderer", "playerBar", "multiMarkersPlayerBarRenderer", "markersMap" })
            if (bar.ValueKind != JsonValueKind.Object || !bar.TryGetProperty(key, out bar)) return Array.Empty<YouTubeChapter>();
        if (bar.ValueKind != JsonValueKind.Array) return Array.Empty<YouTubeChapter>();
        // Prefer creator chapters over automatic chapters when both are present.
        foreach (var kind in new[] { "DESCRIPTION_CHAPTERS", "AUTO_CHAPTERS" })
        foreach (var marker in bar.EnumerateArray().Take(32))
        {
            if (marker.ValueKind != JsonValueKind.Object || !marker.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.String || key.GetString() != kind
                || !marker.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty("chapters", out var chapters) || chapters.ValueKind != JsonValueKind.Array) continue;
            var result = new List<YouTubeChapter>();
            foreach (var entry in chapters.EnumerateArray().Take(MaxChapters))
            {
                if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("chapterRenderer", out var chapter) || chapter.ValueKind != JsonValueKind.Object
                    || !chapter.TryGetProperty("timeRangeStartMillis", out var time) || time.ValueKind != JsonValueKind.Number
                    || !time.TryGetDouble(out var millis) || !double.IsFinite(millis) || millis < 0 || millis >= 604800000
                    || !chapter.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.Object) continue;
                var text = "";
                if (title.TryGetProperty("simpleText", out var simple) && simple.ValueKind == JsonValueKind.String) text = simple.GetString()!;
                else if (title.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array)
                    text = string.Concat(runs.EnumerateArray().Take(32).Where(r => r.ValueKind == JsonValueKind.Object && r.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String).Select(r => r.GetProperty("text").GetString()));
                text = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
                if (text.Length > 0) result.Add(new(millis / 1000, text[..Math.Min(160, text.Length)]));
            }
            if (result.Count > 0) return result.OrderBy(c => c.Start).DistinctBy(c => c.Start).ToArray();
        }
        return Array.Empty<YouTubeChapter>();
    }

    private static string ObjectAssignment(string html, string name)
    {
        var match = Regex.Match(html, @"\bvar\s+" + name + @"\s*=\s*\{", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        if (!match.Success) throw new InvalidDataException("Player metadata missing.");
        var start = match.Index + match.Length - 1;
        var depth = 0; var quoted = false; var escaped = false;
        for (var i = start; i < html.Length; i++)
        {
            var c = html[i];
            if (quoted) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; }
            else if (c == '"') quoted = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return html[start..(i + 1)];
        }
        throw new InvalidDataException("Player metadata incomplete.");
    }
}
