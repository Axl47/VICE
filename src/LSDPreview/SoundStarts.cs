namespace PersonalMods.LSDPreview;

// Observe the game's existing small sound pool, only during an active preview.
// No detours, callbacks or whole-scene discovery are needed. Source membership,
// clip changes and playback restarts identify newly observed sounds.
internal sealed class SoundStarts
{
    internal const int Capacity = 128;
    private readonly record struct State(int Clip, int Sample, bool Playing, uint Frame);
    private readonly Dictionary<int, State> Sources = new();
    private readonly List<int> Departed = new();
    private uint Frame;

    internal void BeginFrame()
    {
        if (++Frame == 0) { Sources.Clear(); Frame = 1; }
    }

    internal bool Observe(int source, int clip, bool playing, int sample)
    {
        if (source == 0 || (playing && (clip == 0 || sample < 0))) return false;
        var exists = Sources.TryGetValue(source, out var previous);
        if (!exists && Sources.Count >= Capacity) return false;
        var started = playing && (!exists || !previous.Playing || previous.Clip != clip || sample < previous.Sample);
        Sources[source] = new State(clip, sample, playing, Frame);
        return started;
    }

    internal void EndFrame()
    {
        Departed.Clear();
        foreach (var pair in Sources) if (pair.Value.Frame != Frame) Departed.Add(pair.Key);
        foreach (var source in Departed) Sources.Remove(source);
    }

    internal void Reset() { Sources.Clear(); Departed.Clear(); Frame = 0; }
}
