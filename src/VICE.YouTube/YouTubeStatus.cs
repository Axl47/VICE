using System.Text.Json;

namespace VICE.YouTube;

internal sealed class YouTubeStatus
{
    internal string Type = "", VideoId = "", Title = "", Message = "";
    internal double Position, Duration;
    internal long Revision;
    internal bool Ready, Paused, Buffering, Ended, Advertisement;
    internal static bool Handshake(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return Text(root, "type") == "ready" && Number(root, "protocol") == YouTubeRules.Protocol
            && Number(root, "width") == YouTubeRules.Width && Number(root, "height") == YouTubeRules.Height
            && Text(root, "audio") == "speakers";
    }
    internal static YouTubeStatus? Parse(string json)
    {
        if (json.Length > 4096) throw new InvalidDataException("Oversized companion message.");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var type = Text(root, "type");
        if (type is not ("state" or "error")) return null;
        return new()
        {
            Type = type, VideoId = Text(root, "videoId"), Title = Text(root, "title"), Message = Text(root, "message"),
            Position = Math.Clamp(Number(root, "position"), 0, 604800), Duration = Math.Clamp(Number(root, "duration"), 0, 604800),
            Revision = (long)Math.Clamp(Number(root, "revision"), 0, int.MaxValue), Ready = Flag(root, "ready"),
            Paused = Flag(root, "paused"), Buffering = Flag(root, "buffering"), Ended = Flag(root, "ended"), Advertisement = Flag(root, "advertisement")
        };
    }
    private static string Text(JsonElement root, string key)
        => root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static bool Flag(JsonElement root, string key)
        => root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    private static double Number(JsonElement root, string key)
        => root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : 0;
}
