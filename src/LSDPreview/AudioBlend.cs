namespace PersonalMods.LSDPreview;

internal static class AudioBlend
{
    internal static float Sample(float elapsed, float amount)
    {
        if (!float.IsFinite(elapsed) || !float.IsFinite(amount)) return 0;
        var wave = 0.5f + 0.5f * MathF.Sin(elapsed * 0.19f - 1.6f);
        var pulse = Math.Clamp((wave - 0.45f) / 0.55f, 0, 1);
        // Normal 0.60 * 0.35 settings now produce a 23.5-29.4% single
        // return. Keep its absolute gain below the unchanged original sound.
        return Math.Min(0.90f, 1.40f * Math.Clamp(amount, 0, 1)) * (0.80f + 0.20f * pulse * pulse);
    }
}
