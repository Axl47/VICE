namespace PersonalMods.LSDPreview;

internal static class AudioBlend
{
    internal static float Sample(float elapsed, float amount)
    {
        if (!float.IsFinite(elapsed) || !float.IsFinite(amount)) return 0;
        var wave = 0.5f + 0.5f * MathF.Sin(elapsed * 0.19f - 1.6f);
        var pulse = Math.Clamp((wave - 0.45f) / 0.55f, 0, 1);
        return 0.5f * Math.Clamp(amount, 0, 1) * pulse * pulse;
    }
}

// A later native snapshot transition invalidates our write. Cleanup must not
// undo an effect that the game has applied since our last contribution.
internal sealed class AudioSnapshotLease
{
    private uint WrittenRevision;
    private bool HasWritten;

    internal void Written(uint revision) { WrittenRevision = revision; HasWritten = true; }
    internal bool CanRestore(uint revision) => HasWritten && WrittenRevision == revision;
    internal void Release() => HasWritten = false;
}
