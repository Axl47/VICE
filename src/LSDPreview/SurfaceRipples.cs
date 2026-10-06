using Il2CppScheduleOne.FX;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Own a native pass/material instead of writing the mushroom feature's state.
internal sealed class SurfaceRipples : IDisposable
{
    private readonly ScriptableRenderer Renderer;
    private PsychedelicFullScreenFeature? Feature;
    private Material? Material;
    private PsychedelicFullScreenFeature.MaterialProperties? Properties;
    private bool Disposed;

    internal SurfaceRipples(Camera camera)
    {
        var data = camera.GetComponent<UniversalAdditionalCameraData>();
        if (!data) throw new InvalidOperationException("The player camera has no URP renderer.");
        Renderer = data.scriptableRenderer;
        try
        {
            PsychedelicFullScreenFeature? source = null;
            foreach (var candidate in Renderer.rendererFeatures)
            {
                source = candidate.TryCast<PsychedelicFullScreenFeature>();
                if (source is not null && source) break;
            }
            // The manager provides a fallback for a temporarily changed renderer.
            source ??= PostProcessingManager.Instance?._psychedelicFullScreenFeature;
            if (source is null || !source || !source.FeatureSettings.passMaterial)
                throw new InvalidOperationException("The game's psychedelic shader is unavailable.");
            Material = new Material(source.FeatureSettings.passMaterial) { name = "Personal LSD ripple material", hideFlags = HideFlags.DontSave };
            Feature = Object.Instantiate(source);
            Feature.name = "Personal LSD surface ripples";
            Feature.hideFlags = HideFlags.DontSave;
            Feature.SetActive(false);
            Properties = new PsychedelicFullScreenFeature.MaterialProperties
            {
                NoiseScale = 14, Blend = 0, PanSpeed = new Vector2(0.035f, 0.022f), DoesBounce = true, Amplitude = 0
            };
            Feature._settings = new PsychedelicFullScreenFeature.Settings
            {
                profilerTag = "Personal LSD ripples", renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
                passMaterial = Material, ActiveProperties = Properties,
                MaterialPresets = new Il2CppSystem.Collections.Generic.List<PsychedelicFullScreenFeature.MaterialPropertyPreset>()
            };
            // The clone has not rendered: the initial pass has allocated no RTs.
            // Recreate against the owned settings and material before attaching.
            Feature.Create();
            Renderer.rendererFeatures.Add(Feature);
        }
        catch { Dispose(); throw; }
    }

    internal void Tick(float elapsed, float amount, float strength, float motion)
    {
        if (Disposed || Feature is null || !Feature || Properties is null) return;
        Properties.Blend = RippleRange.Blend(amount, strength, motion);
        Properties.NoiseScale = 12 + 3 * Mathf.Sin(elapsed * 0.08f);
        Properties.Amplitude = RippleRange.Amplitude(elapsed, strength, motion);
        Feature.SetActive(Properties.Blend > 0.001f);
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        if (Feature is not null && Feature)
        {
            Feature.SetActive(false);
            Renderer.rendererFeatures.Remove(Feature);
            // _source belongs to URP; only the pass's temporary texture is ours.
            Feature._psychedelicPass?._tempTexture?.Release();
            Feature.Dispose();
            Object.Destroy(Feature);
        }
        if (Material is not null && Material) Object.Destroy(Material);
        Feature = null;
        Material = null;
        Properties = null;
    }
}
