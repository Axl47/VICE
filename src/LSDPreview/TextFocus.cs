namespace PersonalMods.LSDPreview;

internal readonly record struct ReadingFocus(float Opacity, float OffsetX, float OffsetY,
    float Softness, float Weight, float ColorMix);

// A slow, smooth loss/recovery of focus, with independent phases per label.
// Text content, glyph order, layout, hit targets and native text color stay intact.
internal static class TextFocus
{
    internal static ReadingFocus Sample(float elapsed, int seed, float strength)
    {
        if (!float.IsFinite(elapsed) || !float.IsFinite(strength) || strength <= 0) return default;
        strength = Math.Clamp(strength, 0, 1);
        var mixed = unchecked((uint)seed * 2654435761u);
        mixed ^= mixed >> 16;
        var phase = (mixed & 1023) * (2 * MathF.PI / 1024);
        var wave = 0.5f + 0.5f * MathF.Sin(elapsed * 0.55f + phase);
        var ramp = Math.Clamp((wave - 0.55f) / 0.35f, 0, 1);
        var focus = strength * ramp * ramp * (3 - 2 * ramp);
        return new ReadingFocus(Math.Min(0.42f, 0.75f * focus),
            focus * (0.65f + 0.25f * MathF.Sin(elapsed * 0.38f + phase)),
            focus * 0.38f * MathF.Cos(elapsed * 0.31f + phase),
            focus * 0.35f, focus * 0.045f * MathF.Sin(elapsed * 0.67f + phase),
            0.5f + 0.5f * MathF.Sin(elapsed * 0.21f + phase));
    }
}
