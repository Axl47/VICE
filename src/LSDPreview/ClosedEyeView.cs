namespace PersonalMods.LSDPreview;

// Input and transition state only; the completed-frame GPU pass owns the image.
internal sealed class ClosedEyeView
{
    internal const float DefaultStrength = 0.55f, CloseSeconds = 0.85f, OpenSeconds = 0.45f;
    private float Progress;
    private bool NeedsRelease;
    internal float Closure => TripTimeline.Smooth(Progress);

    internal static bool CanClose(bool active, bool stopping, bool paused, bool focused, bool typing,
        bool menuOpen, bool canLook, bool hasCameraUi)
        => active && !stopping && !paused && focused && !typing && !menuOpen && canLook && !hasCameraUi;

    internal void Step(float delta, bool held, bool allowed)
    {
        if (!allowed)
        {
            Progress = 0;
            NeedsRelease = held;
            return;
        }
        if (!held) NeedsRelease = false;
        if (!float.IsFinite(delta) || delta <= 0) return;
        var closing = held && !NeedsRelease;
        Progress = Math.Clamp(Progress + (closing ? delta / CloseSeconds : -delta / OpenSeconds), 0, 1);
    }

    // Cleanup must require a fresh hold if B is still down when a trip restarts.
    internal void Reset() { Progress = 0; NeedsRelease = true; }
}
