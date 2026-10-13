using UnityEngine;

namespace VICE.Lifestyle.Television;

// Optional extension contract. Lifestyle never references or loads the add-on DLL.
public sealed record YouTubeChapter(double Start, string Title);
public interface ITVYouTubeProvider
{
    bool IsAvailable { get; }
    string UnavailableReason { get; }
    ITVYouTubeSession Create(string videoId, double now);
}
public interface ITVYouTubeSession : IDisposable
{
    Texture? Texture { get; }
    bool HasMedia { get; }
    bool Failed { get; }
    string Status { get; }
    bool CanToggleCaptions { get; }
    IReadOnlyList<YouTubeChapter> Chapters { get; }
    string ChapterStatus { get; }
    long FrameSequence { get; }
    int ProcessId { get; }
    void Tick(ScreenState state, double now, float gain, float distance);
    void ToggleCaptions();
}
public static class TVYouTube
{
    public const int ApiVersion = 1;
    public static ITVYouTubeProvider? Provider { get; private set; }
    public static bool Available => Provider?.IsAvailable == true;
    public static string UnavailableReason => Provider?.UnavailableReason ?? "Install the optional VICE YouTube add-on and its TV companion to play YouTube.";
    public static bool IsVideoId(string value) => YouTubeRules.IsId(value);
    public static IDisposable Register(ITVYouTubeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (Provider is not null) throw new InvalidOperationException("A TV YouTube provider is already registered.");
        Provider = provider;
        return new Registration(provider);
    }
    internal static void RequireAvailable()
    { if (!Available) throw new InvalidOperationException(UnavailableReason); }
    private sealed class Registration(ITVYouTubeProvider owner) : IDisposable
    {
        private bool Disposed;
        public void Dispose() { if(Disposed)return;Disposed=true;if (ReferenceEquals(Provider, owner)) Provider = null; }
    }
}
