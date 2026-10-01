namespace PersonalMods.BetterPolice;

internal static class GangTuning
{
    internal static int Cooldown(int hours, float multiplier)
    {
        if (hours <= 0) return hours;
        var factor = float.IsFinite(multiplier) ? Math.Clamp(multiplier, 0.25f, 1) : 0.5f;
        return Math.Max(1, (int)Math.Ceiling(hours * (double)factor));
    }

    internal static float Radius(float original, float multiplier)
    {
        if (!float.IsFinite(original) || original <= 0) return original;
        var factor = float.IsFinite(multiplier) ? Math.Clamp(multiplier, 1, 2) : 1.5f;
        return (float)Math.Min(float.MaxValue, original * (double)factor);
    }
}
