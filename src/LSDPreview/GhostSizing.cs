namespace PersonalMods.LSDPreview;

internal static class GhostSizing
{
    // Preserve a normally sized capture. Guard unusual import/bake conventions.
    internal static float Fit(float height)
        => float.IsFinite(height) && height > 0.001f ? Math.Clamp(height, 1.25f, 2.1f) / height : 0;
}
