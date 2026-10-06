namespace PersonalMods.LSDPreview;

internal static class RippleRange
{
    internal const float DefaultStrength = 0.30f;
    private const float OldDefaultStrength = 0.50f, OldAmplitudeScale = 0.12675f;

    // Hold blend at the former default, so the new slider changes displacement
    // linearly. New maximum = 110% of the approved 0.5.0 default look.
    internal static float Blend(float amount, float strength, float motion)
        => Safe(strength) > 0 ? Safe(amount) * OldDefaultStrength * Safe(motion) : 0;

    internal static float Amplitude(float elapsed, float strength, float motion)
    {
        if (!float.IsFinite(elapsed)) return 0;
        var wave = 0.008f + 0.006f * (0.5f + 0.5f * MathF.Sin(elapsed * 0.23f));
        return OldAmplitudeScale * OldDefaultStrength * 1.10f * wave * Safe(strength) * Safe(motion);
    }

    private static float Safe(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
}
