namespace PersonalMods.LSDPreview;

internal enum TripPhase { Inactive, Rising, Peak, Fading }

// A deliberately compressed artistic preview, independent of the game's clock.
internal sealed class TripTimeline
{
    internal const float DefaultDuration = 180;
    internal bool Active { get; private set; }
    internal float Elapsed { get; private set; }
    internal float Duration { get; private set; }
    internal float Envelope { get; private set; }
    internal TripPhase Phase { get; private set; }
    private bool Stopping;
    internal bool IsStopping => Stopping;
    private float StopElapsed, StopEnvelope;
    private const float StopDuration = 2;

    internal void Start(float seconds)
    {
        Reset();
        Duration = Safe(seconds, DefaultDuration, 20, 600);
        Active = true;
        Phase = TripPhase.Rising;
    }

    internal void RequestStop()
    {
        if (!Active || Stopping) return;
        if (Envelope <= 0) { Reset(); return; }
        Stopping = true;
        StopEnvelope = Envelope;
        StopElapsed = 0;
        Phase = TripPhase.Fading;
    }

    internal void Step(float delta, bool suspended = false)
    {
        if (!Active || suspended || !float.IsFinite(delta) || delta <= 0) return;
        Elapsed = Math.Min(Duration, Elapsed + delta);
        if (Stopping)
        {
            StopElapsed = Math.Min(StopDuration, StopElapsed + delta);
            Envelope = StopEnvelope * (1 - Smooth(StopElapsed / StopDuration));
            if (StopElapsed >= StopDuration) Reset();
            return;
        }
        // Use double ratios so exact boundaries such as 15 seconds of a
        // 100-second session do not remain in Rising due to float rounding.
        var attack = Duration * 0.15;
        var release = Duration * 0.25;
        if (Elapsed < attack)
        {
            Phase = TripPhase.Rising;
            Envelope = Smooth((float)(Elapsed / attack));
        }
        else if (Elapsed < Duration - release)
        {
            Phase = TripPhase.Peak;
            Envelope = 1;
        }
        else
        {
            Phase = TripPhase.Fading;
            Envelope = Smooth((float)((Duration - Elapsed) / release));
        }
        if (Elapsed >= Duration) Reset();
    }

    internal void Reset()
    {
        Active = Stopping = false;
        Elapsed = Duration = Envelope = StopElapsed = StopEnvelope = 0;
        Phase = TripPhase.Inactive;
    }

    internal static float Safe(float value, float fallback, float minimum, float maximum)
        => Math.Clamp(float.IsFinite(value) ? value : fallback, minimum, maximum);

    internal static float Smooth(float value)
    {
        value = Safe(value, 0, 0, 1);
        return value * value * (3 - 2 * value);
    }

    // Peripheral figures become indistinct as they approach the center of view.
    internal static float PeripheralVisibility(float gazeDot)
        => 1 - Smooth((Safe(gazeDot, 1, -1, 1) - 0.8f) / 0.15f);
}
