namespace PersonalMods.LSDPreview;

// Pure math and ownership state, also exercised without a running Unity player.
internal readonly record struct SurfaceTint(float Red, float Green, float Blue, float Alpha)
{
    internal bool SameAs(SurfaceTint other)
        => MathF.Abs(Red - other.Red) < 0.00001f && MathF.Abs(Green - other.Green) < 0.00001f
            && MathF.Abs(Blue - other.Blue) < 0.00001f && MathF.Abs(Alpha - other.Alpha) < 0.00001f;
}

internal sealed class TintLease
{
    internal SurfaceTint Baseline { get; private set; }
    private SurfaceTint LastWritten;
    private bool HasWritten;

    internal TintLease(SurfaceTint baseline) => Baseline = baseline;

    internal void Observe(SurfaceTint current)
    {
        // A weather/daylight/native update becomes the new baseline. Do not
        // accumulate our previous modulation or undo another system's write.
        if (!HasWritten || !current.SameAs(LastWritten)) Baseline = current;
    }

    internal void Written(SurfaceTint tint) { LastWritten = tint; HasWritten = true; }
    internal bool CanRestore(SurfaceTint current) => HasWritten && current.SameAs(LastWritten);
}

internal static class ColorBreathing
{
    internal static SurfaceTint Sample(SurfaceTint baseline, int seed, float elapsed, float amount)
    {
        if (!float.IsFinite(elapsed) || !float.IsFinite(amount) || amount <= 0) return baseline;
        amount = Math.Clamp(amount, 0, 1);
        var hash = unchecked((uint)seed * 2654435761u);
        hash ^= hash >> 16;
        var phase = (hash & 65535) / 65535f * MathF.PI * 2;
        var speed = 0.18f + ((hash >> 16) & 255) / 255f * 0.12f;
        var time = elapsed * speed;
        var light = 0.03f * MathF.Sin(time * 0.43f + phase);
        var red = 1 + amount * (light + 0.18f * MathF.Sin(time + phase));
        var green = 1 + amount * (light + 0.18f * MathF.Sin(time * 0.87f + phase + 2.1f));
        var blue = 1 + amount * (light + 0.18f * MathF.Sin(time * 1.13f + phase + 4.2f));
        return new SurfaceTint(baseline.Red * red, baseline.Green * green, baseline.Blue * blue, baseline.Alpha);
    }

    internal static float EchoWetMix(float elapsed, float amount)
    {
        if (!float.IsFinite(elapsed) || !float.IsFinite(amount)) return 0;
        var wave = 0.5f + 0.5f * MathF.Sin(elapsed * 0.19f - 1.6f);
        var pulse = Math.Clamp((wave - 0.45f) / 0.55f, 0, 1);
        return 0.06f * Math.Clamp(amount, 0, 1) * pulse * pulse;
    }
}
