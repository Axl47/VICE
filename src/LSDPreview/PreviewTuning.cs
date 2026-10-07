namespace PersonalMods.LSDPreview;

// The approved 0.10.1 settings remain the reference, so a stronger default
// does not silently recalibrate lens breathing or prevent a baseline replay.
internal static class PreviewTuning
{
    internal const float BaselineRipple = 0.30f, BaselineAudio = 0.35f;
    internal const float DefaultRipple = BaselineRipple * 1.25f,
        DefaultAudio = BaselineAudio * 1.25f, DefaultTextFocus = 0.50f;
}
