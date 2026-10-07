using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PersonalMods.TV;

public sealed class RelayItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public int? Year { get; set; }
    public int Number { get; set; }
    public int SeasonNumber { get; set; }
    public string MediaType { get; set; } = "";
    public bool Playable { get; set; }
    public string? Reason { get; set; }
    public List<RelayItem> Versions { get; set; } = new();
}
public sealed class RelayList { public List<RelayItem> Items { get; set; } = new(); }
public sealed class RelayTitle { public string Id { get; set; } = ""; public string Title { get; set; } = ""; public List<RelayItem> Seasons { get; set; } = new(); }
public sealed class RelayCapabilities { public int ProtocolVersion { get; set; } public List<string> Profiles { get; set; } = new(); public List<string> SubtitleModes { get; set; } = new(); }
public sealed class RelayPlayback
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public string Title { get; set; } = "";
    public string EpisodeId { get; set; } = "";
    public string? SourceId { get; set; }
    public string? VersionId { get; set; }
    public string Profile { get; set; } = "";
    public string SubtitleMode { get; set; } = "";
    public string? MediaUrl { get; set; }
    public string? MimeType { get; set; }
    public double? DurationSeconds { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool Seekable { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    internal void Validate(string episode)
    {
        if (Id.Length is < 1 or > 256 || EpisodeId != episode || Status is not ("preparing" or "ready" or "failed" or "unsupported" or "expired"))
            throw new InvalidDataException("Relay returned an invalid playback identity or status.");
        if (Status == "ready")
        {
            MediaRules.VideoUrl(MediaUrl ?? "");
            if (MimeType != "video/mp4" || Profile != "mp4-h264-aac" || !Seekable || ExpiresAt is null || ExpiresAt <= DateTimeOffset.UtcNow)
                throw new InvalidDataException("Relay did not provide a ready, seekable compatible MP4.");
            if (DurationSeconds is { } duration && (!double.IsFinite(duration) || duration is <= 0 or > 604800))
                throw new InvalidDataException("Relay returned an invalid duration.");
        }
    }
    internal void ValidateSelection(MediaSelection media)
    {
        Validate(media.Source);
        if(Status=="ready"&&(SubtitleMode!=media.SubtitleMode||(media.SourceId is not null&&SourceId!=media.SourceId)||(media.VersionId is not null&&VersionId!=media.VersionId)))
            throw new InvalidDataException("Relay changed the requested source, version or subtitle mode.");
    }
}

internal sealed class RelayClient : IDisposable
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    private readonly HttpClient Client;
    private readonly Uri Root;
    internal RelayClient(string origin, string token, HttpMessageHandler? handler = null)
    {
        var uri = MediaRules.VideoUrl(origin.TrimEnd('/') + "/");
        if (uri.Scheme != "https" && !uri.IsLoopback) throw new InvalidDataException("Use HTTPS for Relay, or loopback HTTP for local development.");
        if (uri.Query.Length > 0 || uri.Fragment.Length > 0 || string.IsNullOrWhiteSpace(token)) throw new InvalidDataException("Set Relay's API origin and scoped game token first.");
        Root = new Uri(uri, "integrations/game/v1/");
        Client = handler is null ? new HttpClient() : new HttpClient(handler);
        Client.Timeout = TimeSpan.FromSeconds(60);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
    private async Task<T> Send<T>(HttpMethod method, string path, object? body, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, new Uri(Root, path));
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (response.Content.Headers.ContentLength > 512 * 1024) throw new InvalidDataException("Relay response is too large.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellation);
        using var output = new MemoryStream(); var buffer = new byte[8192];
        while (true) { var count = await input.ReadAsync(buffer, cancellation); if (count == 0) break; if (output.Length + count > 512 * 1024) throw new InvalidDataException("Relay response is too large."); output.Write(buffer, 0, count); }
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            { System.Net.HttpStatusCode.Unauthorized => "Relay token is missing or expired.", System.Net.HttpStatusCode.Forbidden => "Relay denied this request.", System.Net.HttpStatusCode.NotFound => "Relay's game API or requested media is unavailable.", System.Net.HttpStatusCode.Conflict => "Relay rejected this playback selection or request. Choose the episode again.", System.Net.HttpStatusCode.TooManyRequests => "Relay reached its request or active playback limit. Stop unused playbacks, or try again shortly.", System.Net.HttpStatusCode.ServiceUnavailable => "Relay's game playback service is temporarily unavailable.", _ => $"Relay request failed ({(int)response.StatusCode})." };
            throw new InvalidOperationException(message);
        }
        if (typeof(T) == typeof(bool)) return (T)(object)true;
        if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
            throw new InvalidDataException("Relay returned a page instead of its game API response.");
        return JsonSerializer.Deserialize<T>(output.ToArray(), Json) ?? throw new InvalidDataException("Relay returned an empty response.");
    }
    internal async Task<string> Query(string operation, string value, int season, CancellationToken cancellation)
    {
        if (value.Length > (operation=="search"?200:256)) throw new InvalidDataException("Relay query is too long.");
        object result = operation switch
        {
            "capabilities" => await Send<RelayCapabilities>(HttpMethod.Get, "capabilities", null, cancellation),
            "search" => await Send<RelayList>(HttpMethod.Get, "search?q=" + Uri.EscapeDataString(value) + "&limit=20", null, cancellation),
            "title" => await Send<RelayTitle>(HttpMethod.Get, "titles/" + Uri.EscapeDataString(value), null, cancellation),
            "episodes" => await Send<RelayList>(HttpMethod.Get, "titles/" + Uri.EscapeDataString(value) + "/episodes?seasonNumber=" + Math.Clamp(season, 0, 1000), null, cancellation),
            "sources" => await Send<RelayList>(HttpMethod.Get, "episodes/" + Uri.EscapeDataString(value) + "/sources", null, cancellation),
            _ => throw new InvalidDataException("Unknown Relay operation.")
        };
        var json = JsonSerializer.Serialize(result, Json);
        if (json.Length > 60000) throw new InvalidDataException("Relay catalog results are too large.");
        return json;
    }
    internal Task<RelayPlayback> Create(MediaSelection media, string requestId, CancellationToken cancellation) => Send<RelayPlayback>(HttpMethod.Post, "playbacks", new {requestId, episodeId=media.Source, sourceId=media.SourceId, versionId=media.VersionId, profile="mp4-h264-aac", subtitleMode=media.SubtitleMode}, cancellation);
    internal Task<RelayPlayback> Get(string id, CancellationToken cancellation) => Send<RelayPlayback>(HttpMethod.Get, "playbacks/" + Uri.EscapeDataString(id), null, cancellation);
    internal Task<RelayPlayback> Refresh(string id, double position, CancellationToken cancellation) => Send<RelayPlayback>(HttpMethod.Post, "playbacks/" + Uri.EscapeDataString(id) + "/refresh", new {positionSeconds=position}, cancellation);
    internal Task<bool> Stop(string id, CancellationToken cancellation) => Send<bool>(HttpMethod.Post, "playbacks/" + Uri.EscapeDataString(id) + "/stop", new {}, cancellation);
    public void Dispose() => Client.Dispose();
}
