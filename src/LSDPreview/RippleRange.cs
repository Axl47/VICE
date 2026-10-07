namespace PersonalMods.LSDPreview;

internal static class RippleRange
{
    internal const float DefaultStrength = 0.30f, DefaultCenterDegrees = 5, MaximumPixels = 18;
    private const float ReferencePixels = 60;

    // Actual screen displacement is bounded in pixels, independently of noise
    // panning. The old native _Amplitude changes noise coordinates, not warp size.
    internal static float Pixels(float elapsed, float amount, float strength, float motion, float movement)
    {
        if (!float.IsFinite(elapsed)) return 0;
        var breath = 0.85f + 0.15f * MathF.Sin(elapsed * 0.23f);
        // The previous calibration multiplied four small defaults into a
        // subpixel result, then masked it again. Restore a few visible pixels
        // at normal settings while keeping a separate absolute guard.
        var pixels = Math.Min(MaximumPixels, ReferencePixels * Safe(amount) * Safe(strength) * Safe(motion));
        return pixels * Safe(movement) * breath;
    }

    internal static float FocusRadius(float degrees, float verticalFov)
    {
        degrees = TripTimeline.Safe(degrees, DefaultCenterDegrees, 0, 25);
        verticalFov = TripTimeline.Safe(verticalFov, 65, 20, 140);
        return MathF.Tan(degrees * MathF.PI / 180) / (2 * MathF.Tan(verticalFov * MathF.PI / 360));
    }

    internal static float FocusMask(float radius, float inner, float outer)
    {
        if (!float.IsFinite(radius) || !float.IsFinite(inner) || !float.IsFinite(outer) || outer <= inner) return 0;
        var t = Math.Clamp((radius - inner) / (outer - inner), 0, 1);
        return t * t * (3 - 2 * t);
    }

    internal static float FocusOuter(float inner) => Math.Max(inner + 0.12f, 0.28f);

    // Restore a restrained version of the previously visible native lens
    // breathing, independently of the small flowing shader. Volume weight
    // applies overall intensity; scaling here includes motion/ripple/comfort.
    internal static float LensBreathing(float elapsed, float strength, float motion, float movement)
        => float.IsFinite(elapsed)
            ? Math.Min(0.11f, 0.11f * Safe(strength) / DefaultStrength * Safe(motion))
                * Safe(movement) * MathF.Sin(elapsed * 0.65f)
            : 0;

    private static float Safe(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
}

// Smoothly reduce bending while travelling or turning. Color breathing keeps
// its own timing and strength; slowing down restores the stationary effect.
internal sealed class MovementComfort
{
    internal float Factor { get; private set; } = 1;
    internal float Step(float seconds, float speed, float degreesPerSecond)
    {
        if (!float.IsFinite(seconds) || seconds <= 0) return Factor;
        if (!float.IsFinite(speed) || !float.IsFinite(degreesPerSecond)) return Factor;
        var activity = Math.Max(Math.Max(0, speed) / 4, Math.Abs(degreesPerSecond) / 100);
        var target = 0.25f + 0.75f / (1 + activity * activity);
        Factor += (target - Factor) * (1 - MathF.Exp(-Math.Min(seconds, 1) / 0.3f));
        return Factor;
    }
}
