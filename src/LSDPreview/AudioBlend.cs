namespace PersonalMods.LSDPreview;

internal static class AudioBlend
{
    internal static float Sample(float elapsed, float amount)
    {
        if (!float.IsFinite(elapsed) || !float.IsFinite(amount)) return 0;
        var wave = 0.5f + 0.5f * MathF.Sin(elapsed * 0.19f - 1.6f);
        var pulse = Math.Clamp((wave - 0.45f) / 0.55f, 0, 1);
        // A single delayed copy, with no feedback or capture warm-up. Give
        // the normal 0.60 * 0.35 setting a 14.2-18.9% return, before the
        // original source volume and native mixer/spatial attenuation.
        return 0.90f * Math.Clamp(amount, 0, 1) * (0.75f + 0.25f * pulse * pulse);
    }
}
