namespace PersonalMods.LSDPreview;

internal static class AudioBlend
{
    internal static float Sample(float elapsed, float amount, float closedEyes = 0)
    {
        if (!float.IsFinite(elapsed) || !float.IsFinite(amount)) return 0;
        var wave = 0.5f + 0.5f * MathF.Sin(elapsed * 0.19f - 1.6f);
        var pulse = Math.Clamp((wave - 0.45f) / 0.55f, 0, 1);
        // Normal 0.60 * 0.4375 settings produce a 29.4-36.75% single
        // return. Keep its absolute gain below the unchanged original sound.
        var wet = Math.Min(0.90f, 1.40f * Math.Clamp(amount, 0, 1)) * (0.80f + 0.20f * pulse * pulse);
        var closure = float.IsFinite(closedEyes) ? Math.Clamp(closedEyes, 0, 1) : 0;
        // Follow the smoothed eyelid transition, retaining the absolute wet cap.
        return Math.Min(0.90f, wet * (1 + 0.45f * closure));
    }
}
