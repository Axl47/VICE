namespace VICE.Lifestyle.Television;

internal interface IYouTubeBridge : IDisposable
{
    bool IsReady { get; }
    string Error { get; }
    void Send(object command);
    bool TryStatus(out YouTubeStatus status);
}

// Browser reports own the single-player timeline; buffering never advances a synthetic clock.
internal sealed class YouTubePlayback : IDisposable
{
    private readonly IYouTubeBridge Bridge;
    private readonly double Started;
    private long UiRevision = -1, CommandRevision;
    private bool LoadSent, WantedPlaying;
    private double ObservedPosition;
    private int Gain = -1;
    internal string Status { get; private set; } = "Preparing YouTube";
    internal bool HasMedia { get; private set; }
    internal bool Failed { get; private set; }
    internal YouTubePlayback(IYouTubeBridge bridge, double now) { Bridge = bridge; Started = now; }
    internal void Tick(ScreenState state, double now, double gain)
    {
        if (Bridge.Error.Length > 0) { Fail(Bridge.Error); return; }
        if (!Bridge.IsReady)
        {
            if (now - Started > 45) Fail("The YouTube companion did not become ready. Press Retry.");
            return;
        }
        var newGain = (int)Math.Round(Math.Clamp(gain, 0, 1) * 1000);
        if (newGain != Gain) { Gain = newGain; Bridge.Send(new { op = "gain", value = Gain }); }
        if (!LoadSent)
        {
            LoadSent = true; UiRevision = state.Revision; WantedPlaying = state.Playing; ObservedPosition = state.Position;
            Bridge.Send(new { op = "load", videoId = state.Media.Source, position = state.Position, paused = !state.Playing, revision = ++CommandRevision });
        }
        else if (UiRevision != state.Revision)
        {
            UiRevision = state.Revision;
            if (Math.Abs(state.Position - ObservedPosition) > .01)
                Bridge.Send(new { op = "seek", position = state.Position, revision = ++CommandRevision });
            if (WantedPlaying != state.Playing)
            {
                WantedPlaying = state.Playing;
                Bridge.Send(new { op = state.Playing ? "play" : "pause", position = state.Position, revision = ++CommandRevision });
            }
            ObservedPosition = state.Position;
        }
        while (Bridge.TryStatus(out var report))
        {
            if (report.Type == "error") { Status = "YouTube could not play this video. Press Retry or choose another link."; continue; }
            // In-flight reports from before a pause/seek must not undo the user's command.
            if (report.Revision != CommandRevision || report.VideoId != state.Media.Source) continue;
            if (report.Advertisement) { Status = "YouTube advertisement"; continue; }
            if (!report.Ready) { Status = "Waiting for YouTube"; continue; }
            if (!HasMedia)
            {
                // The installed companion renders the watch page. Its documented input
                // commands can put the ready player into fullscreen within that surface.
                Bridge.Send(new { op = "focus", focused = true });
                Bridge.Send(new { op = "key", type = "down", code = 70 });
                Bridge.Send(new { op = "key", type = "char", code = 102 });
                Bridge.Send(new { op = "key", type = "up", code = 70 });
            }
            HasMedia = true;
            state.Position = ObservedPosition = report.Position; state.Anchor = now; state.Duration = report.Duration;
            state.Playing = WantedPlaying = !report.Paused && !report.Ended;
            if (report.Title.Length > 0) state.Media.Title = report.Title[..Math.Min(160, report.Title.Length)];
            Status = report.Ended ? "YouTube finished" : report.Buffering ? "YouTube buffering" : report.Paused ? "YouTube paused" : "Playing YouTube";
        }
        if (!HasMedia && now - Started > 120) Fail("YouTube did not start. Check the internet connection and press Retry.");
    }
    private void Fail(string message) { Failed = true; Status = message; }
    public void Dispose() => Bridge.Dispose();
}
