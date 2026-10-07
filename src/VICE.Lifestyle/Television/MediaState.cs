using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace VICE.Lifestyle.Television;

public sealed class MediaSelection
{
    public string Kind { get; set; } = "none";
    public string Source { get; set; } = "";
    public string Title { get; set; } = "";
    public string? SourceId { get; set; }
    public string? VersionId { get; set; }
    public string SubtitleMode { get; set; } = "burned";
    public MediaSelection Clone() => JsonSerializer.Deserialize<MediaSelection>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        if (Kind is not ("none" or "image" or "url" or "relay" or "local")) throw new InvalidDataException("Unknown media type.");
        if (Title.Length > 160 || Source.Length > 4096 || SourceId?.Length > 256 || VersionId?.Length > 256)
            throw new InvalidDataException("Media selection is too long.");
        if (Kind == "image" && !MediaRules.IsHash(Source)) throw new InvalidDataException("Invalid image identity.");
        if (Kind == "url") MediaRules.VideoUrl(Source);
        if (Kind == "local") MediaRules.FileName(Source, ".mp4");
        if (Kind == "relay" && (string.IsNullOrWhiteSpace(Source) || Source.Length > 256 || SubtitleMode is not ("burned" or "none")))
            throw new InvalidDataException("Invalid Relay episode selection.");
    }
}

public sealed class ScreenState
{
    public string Id { get; set; } = "";
    public long Revision { get; set; }
    public int MediaGeneration { get; set; }
    public MediaSelection Media { get; set; } = new();
    public bool Playing { get; set; }
    public double Position { get; set; }
    public double Anchor { get; set; }
    public double Duration { get; set; }
    public float Volume { get; set; } = 0.65f;
    public bool Remember { get; set; }
    public string PlaybackId { get; set; } = "";
    public string PlaybackUrl { get; set; } = "";
    public DateTimeOffset? ExpiresAt { get; set; }
    public string Status { get; set; } = "";
    public double Target(double hostTime) => Math.Clamp(Position + (Playing ? Math.Max(0, hostTime - Anchor) : 0), 0, Duration > 0 ? Duration : 604800);
    public void Reanchor(double hostTime) { Position = Target(hostTime); Anchor = hostTime; }
    public ScreenState Clone() => JsonSerializer.Deserialize<ScreenState>(JsonSerializer.Serialize(this))!;
    public ScreenState ForSave()
    {
        var saved = Clone();
        if (Media.Kind != "image" && !Remember) saved.Media = new();
        saved.Playing = false; saved.Position = 0; saved.Anchor = 0;
        saved.PlaybackId = ""; saved.PlaybackUrl = ""; saved.ExpiresAt = null; saved.Status = "";
        return saved;
    }
    public void Validate()
    {
        if (!Guid.TryParse(Id, out _) || Revision < 0 || MediaGeneration < 0 || !double.IsFinite(Position) || Position is < 0 or > 604800
            || !double.IsFinite(Anchor) || !double.IsFinite(Duration) || Duration is < 0 or > 604800
            || !float.IsFinite(Volume) || Volume is < 0 or > 1 || Status.Length > 256 || PlaybackId.Length > 256)
            throw new InvalidDataException("Invalid screen state.");
        Media.Validate();
        if (PlaybackUrl.Length > 0) MediaRules.VideoUrl(PlaybackUrl);
    }
}

public sealed class TVDocument
{
    public int Version { get; set; } = 1;
    public List<ScreenState> Screens { get; set; } = new();
    public List<MediaSelection> Favorites { get; set; } = new();
    public void Validate()
    {
        if (Version != 1 || Screens.Count > 64 || Favorites.Count > 32 || Screens.Select(s => s.Id).Distinct().Count() != Screens.Count)
            throw new InvalidDataException("Unsupported or invalid TV save.");
        foreach (var screen in Screens) screen.Validate();
        foreach (var favorite in Favorites) favorite.Validate();
    }
    public string SaveJson() => JsonSerializer.Serialize(new TVDocument { Screens = Screens.Select(s => s.ForSave()).ToList(), Favorites = Favorites.Select(f => f.Clone()).ToList() }, new JsonSerializerOptions { WriteIndented = true });
    public static TVDocument Load(string json, double hostTime)
    {
        if (json.Length > 512 * 1024) throw new InvalidDataException("TV save is too large.");
        var document = JsonSerializer.Deserialize<TVDocument>(json) ?? throw new InvalidDataException("Empty TV save.");
        document.Validate();
        // Never trust previously stored session/grant/timing fields, even in an older save.
        document.Screens = document.Screens.Select(s => s.ForSave()).ToList();
        foreach (var screen in document.Screens) screen.Anchor = hostTime;
        return document;
    }
}

internal static class MediaRules
{
    internal const int MaxImageBytes = 4 * 1024 * 1024, ChunkBytes = 16384, MaxWireChars = 96000;
    internal static bool IsHash(string value) => Regex.IsMatch(value, "^[a-f0-9]{64}$");
    internal static Uri VideoUrl(string value)
    {
        if (value.Length > 4096 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || uri.UserInfo.Length > 0)
            throw new InvalidDataException("Use a direct HTTP(S) video URL without embedded login credentials.");
        return uri;
    }
    internal static string FileName(string value, string extension)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 180 || value != Path.GetFileName(value) || value.IndexOfAny(new[] {'/', '\\', ':', '\0'}) >= 0
            || !Path.GetExtension(value).Equals(extension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a file from the designated media folder.");
        return value;
    }
}

internal sealed class PlaybackClock
{
    private double Offset, BestRoundTrip = double.MaxValue;
    private bool Initialized;
    internal static double LocalNow => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    internal double Now => LocalNow + Offset;
    internal void Observe(double hostTime, double sentAt, double receivedAt)
    {
        var trip = receivedAt - sentAt;
        if (!double.IsFinite(hostTime) || trip is < 0 or > 10) return;
        if (!Initialized || trip <= BestRoundTrip + 0.01)
        { Offset = hostTime - (sentAt + receivedAt) / 2; BestRoundTrip = Math.Min(BestRoundTrip, trip); Initialized = true; }
    }
    internal void Seed(double hostTime) { if (!Initialized) { Offset = hostTime - LocalNow; Initialized = true; } }
    internal void Reset() { Offset = 0; BestRoundTrip = double.MaxValue; Initialized = false; }
}
