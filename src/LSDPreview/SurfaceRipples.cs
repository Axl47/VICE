using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.FX;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Own a native pass/material instead of writing the mushroom feature's state.
internal sealed class SurfaceRipples : IDisposable
{
    private readonly ScriptableRenderer Renderer;
    private readonly Camera Camera;
    private Il2CppAssetBundle? Bundle;
    private bool Peripheral;
    private PsychedelicFullScreenFeature? Feature;
    private Material? Material;
    private PsychedelicFullScreenFeature.MaterialProperties? Properties;
    private bool Disposed, Reported;

    internal SurfaceRipples(Camera camera)
    {
        Camera = camera;
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
            LoadPeripheralShader();
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

    private void LoadPeripheralShader()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Personal.LSDPreview.Peripheral.bundle")
                ?? throw new InvalidOperationException("Authored peripheral shader resource is missing.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            Bundle = Il2CppAssetBundleManager.LoadFromMemory(new Il2CppStructArray<byte>(memory.ToArray()));
            if (Bundle is null) throw new InvalidOperationException("Unity could not load the peripheral shader bundle.");
            var shader = Bundle.LoadAsset<Shader>("assets/personal/lsdperipheral.shader");
            if (!shader || !shader.isSupported) throw new InvalidOperationException("The peripheral shader requires Direct3D 11.");
            Material!.shader = shader;
            Peripheral = true;
            MelonLoader.MelonLogger.Msg("LSD peripheral shader loaded: pixel-bounded displacement with a clear gaze center.");
        }
        catch (Exception error)
        {
            ModSupport.WarnOnce("lsd:peripheral", $"LSD peripheral shader unavailable; using a very small native warp without a center mask: {error.GetBaseException().Message}");
        }
    }

    internal void Tick(float elapsed, float amount, float strength, float motion, float movement)
    {
        if (Disposed || Feature is null || !Feature || Properties is null) return;
        var pixels = RippleRange.Pixels(elapsed, amount, strength, motion, movement);
        if (Peripheral)
        {
            var inner = RippleRange.FocusRadius(LSDPreviewMod.CenterClarityDegrees.Value, Camera.fieldOfView);
            Properties.Blend = pixels / Mathf.Max(1, Camera.pixelHeight);
            Properties.NoiseScale = Mathf.Max(0.1f, Camera.aspect);
            Properties.Amplitude = elapsed;
            Properties.PanSpeed = new Vector2(inner, RippleRange.FocusOuter(inner));
            Properties.DoesBounce = true;
        }
        else
        {
            // Native Blend lerps UVs toward a scalar noise value. Its maximum
            // vector travel is sqrt(width^2+height^2); cap that in pixels too.
            Properties.Blend = pixels / Mathf.Max(1, Mathf.Sqrt(Camera.pixelWidth * (float)Camera.pixelWidth
                + Camera.pixelHeight * (float)Camera.pixelHeight));
            Properties.NoiseScale = 12 + 3 * Mathf.Sin(elapsed * 0.08f);
            Properties.Amplitude = 0.001f;
            Properties.PanSpeed = new Vector2(0.035f, 0.022f);
        }
        Feature.SetActive(pixels > 0.001f);
        if (!Reported && elapsed >= 10)
        {
            Reported = true;
            var pass = Feature._psychedelicPass;
            var rendering = pass is not null && pass._source is not null && pass._tempTexture is not null;
            var written = Material!.GetFloat("_Blend");
            MelonLoader.MelonLogger.Msg($"LSD ripple check: pass allocated {rendering}, active {Feature.isActive}, bound {pixels:F2}px, material blend {written:F7}, elapsed {Material.GetFloat("_Amplitude"):F2}s, focus {Properties.PanSpeed.x:F3}-{Properties.PanSpeed.y:F3}.");
            if (pixels > 0.001f && (!rendering || written <= 0))
                ModSupport.WarnOnce("lsd:ripple:render", "LSD ripple pass has not verified a rendered material update; retain the ripple check line for diagnosis.");
        }
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
        Bundle?.Unload(true);
        Bundle = null;
        Feature = null;
        Material = null;
        Properties = null;
    }
}
