using Il2CppScheduleOne.FX;
using S1LSD.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PersonalMods.S1LSDComparison;

internal sealed class ComparisonVolume : IDisposable
{
    private GameObject? Root;
    private VolumeProfile? Profile;
    private ColorAdjustments Colors = null!;
    private WhiteBalance White = null!;
    private SplitToning Split = null!;
    private Bloom Bloom = null!;
    private Vignette Vignette = null!;
    private ChromaticAberration Chroma = null!;
    private LensDistortion Lens = null!;
    private FilmGrain Grain = null!;

    internal ComparisonVolume(Camera camera)
    {
        try {
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (!data || !data.renderPostProcessing || data.volumeLayerMask.value == 0)
                throw new InvalidOperationException("This camera has no enabled native post-processing volume layer.");
            var layer = Enumerable.Range(0, 32).First(i => (data.volumeLayerMask.value & (1 << i)) != 0);
            Root = new GameObject("Disposable S1LSD comparison volume") { layer = layer, hideFlags = HideFlags.DontSave };
            Root.SetActive(false);
            Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Profile.hideFlags = HideFlags.DontSave;
            Colors = Profile.Add<ColorAdjustments>(true); White = Profile.Add<WhiteBalance>(true);
            Split = Profile.Add<SplitToning>(true); Bloom = Profile.Add<Bloom>(false);
            Vignette = Profile.Add<Vignette>(false); Chroma = Profile.Add<ChromaticAberration>(false);
            Lens = Profile.Add<LensDistortion>(false); Grain = Profile.Add<FilmGrain>(false);
            Bloom.intensity.overrideState = Bloom.threshold.overrideState = Bloom.tint.overrideState = true;
            Vignette.intensity.overrideState = Vignette.smoothness.overrideState = true;
            Chroma.intensity.overrideState = true;
            Lens.intensity.overrideState = Lens.scale.overrideState = true;
            Grain.intensity.overrideState = true;
            var volume = Root.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 9999; volume.sharedProfile = Profile; volume.weight = 1;
            Root.SetActive(true);
        } catch { Dispose(); throw; }
    }

    internal void Tick(in VisualTargets t)
    {
        var game = PostProcessingManager.Instance;
        Colors.postExposure.value = (game && game.colorAdjustments ? game.colorAdjustments.postExposure.value : 0) + t.PostExposure;
        Colors.saturation.value = Mathf.Clamp((game && game.colorAdjustments ? game.colorAdjustments.saturation.value : 0) + t.Saturation, -100, 100);
        Colors.contrast.value = Mathf.Clamp((game && game.colorAdjustments ? game.colorAdjustments.contrast.value : 0) + t.Contrast, -100, 100);
        Colors.hueShift.value = (game && game.colorAdjustments ? game.colorAdjustments.hueShift.value : 0) + t.HueShift;
        White.temperature.value = t.Temperature; White.tint.value = t.Tint;
        Split.shadows.value = new Color(t.Shadows.R, t.Shadows.G, t.Shadows.B);
        Split.highlights.value = new Color(t.Highlights.R, t.Highlights.G, t.Highlights.B);
        Split.balance.value = t.SplitBalance;
        Bloom.intensity.value = (game && game.bloom ? game.bloom.intensity.value : 1) * t.BloomGain;
        Bloom.threshold.value = Mathf.Max(0.5f, (game && game.bloom ? game.bloom.threshold.value : 1) - t.BloomThresholdDrop);
        Bloom.tint.value = new Color(t.BloomTint.R, t.BloomTint.G, t.BloomTint.B);
        Vignette.intensity.value = Mathf.Max(game && game.vig ? game.vig.intensity.value : 0, t.VignetteIntensity);
        Vignette.smoothness.value = t.VignetteSmoothness;
        Chroma.intensity.value = Mathf.Clamp01((game && game.chromaticAberration ? game.chromaticAberration.intensity.value : 0) + t.Chroma);
        Lens.intensity.value = t.LensDistortion; Lens.scale.value = 1 - Mathf.Abs(t.LensDistortion) * 0.15f;
        Grain.intensity.value = t.FilmGrain;
    }

    public void Dispose()
    {
        if (Root) { Root!.SetActive(false); Object.Destroy(Root); }
        if (Profile) {
            foreach (var component in Profile!.components) if (component) Object.Destroy(component);
            Object.Destroy(Profile);
        }
        Root = null; Profile = null;
    }
}
