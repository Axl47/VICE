namespace PersonalMods.LSDPreview;

internal static class AudioBlend
{
    internal static float Sample(float elapsed, float amount)
    {
        if (!float.IsFinite(elapsed) || !float.IsFinite(amount)) return 0;
        var wave = 0.5f + 0.5f * MathF.Sin(elapsed * 0.19f - 1.6f);
        var pulse = Math.Clamp((wave - 0.45f) / 0.55f, 0, 1);
        // Keep an audible low bed, with occasional stronger repeats. Since
        // listener capture includes our own return, gain < 1 also bounds feedback.
        return 0.35f * Math.Clamp(amount, 0, 1) * (0.3f + 0.7f * pulse * pulse);
    }
}
