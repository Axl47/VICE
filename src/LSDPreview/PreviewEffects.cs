using Il2CppBeautify.Universal;
using Il2CppScheduleOne.FX;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

internal sealed class PreviewEffects : IDisposable
{
    private readonly Camera Camera;
    private GameObject? Root;
    private Volume? Volume;
    private VolumeProfile? Profile;
    private ColorAdjustments? Colors;
    private ChromaticAberration? Chromatic;
    private LensDistortion? Breathing;
    private readonly MovementComfort Comfort = new();
    private Vector3 LastPosition;
    private Quaternion LastRotation;
    private bool TrackedMotion;
    private Beautify? Cartoon;
    private SurfaceRipples? Ripples;
    private PhoneColorBreathing? PhoneColors;
    private PreviewAudio? Audio;
    private Afterimages? Trails;
    private HudTextBlur? Text;
    private float BaseSharpen = 1;
    private WorldHallucinations? World;
    private bool Disposed, AttemptedWorld, AttemptedRipples, AttemptedColors, AttemptedAudio, AttemptedTrails, AttemptedText;

    internal PreviewEffects(Camera camera)
    {
        Camera = camera;
        try
        {
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (!data || !data.renderPostProcessing || data.volumeLayerMask.value == 0)
                throw new InvalidOperationException("The player camera needs native post-processing enabled for this preview.");
            var mask = data.volumeLayerMask.value;
            var layer = Enumerable.Range(0, 32).First(i => (mask & (1 << i)) != 0);
            Root = new GameObject("Personal LSD preview volume") { hideFlags = HideFlags.DontSave, layer = layer };
            Root.SetActive(false);
            Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Profile.name = "Personal LSD runtime profile";
            Profile.hideFlags = HideFlags.DontSave;
            Colors = Profile.Add<ColorAdjustments>(false);
            Colors.saturation.overrideState = Colors.hueShift.overrideState = Colors.colorFilter.overrideState
                = Colors.contrast.overrideState = Colors.postExposure.overrideState = true;
            Chromatic = Profile.Add<ChromaticAberration>(false);
            Chromatic.intensity.overrideState = true;
            Breathing = Profile.Add<LensDistortion>(false);
            Breathing.intensity.overrideState = Breathing.center.overrideState = Breathing.scale.overrideState
                = Breathing.xMultiplier.overrideState = Breathing.yMultiplier.overrideState = true;
            Breathing.center.value = new Vector2(0.5f, 0.5f);
            Breathing.scale.value = 1;
            Breathing.xMultiplier.value = Breathing.yMultiplier.value = 1;
            Cartoon = Profile.Add<Beautify>(false);
            var native = PostProcessingManager.Instance;
            if (native && native.beautifySettings) BaseSharpen = native.beautifySettings.sharpenIntensity.value;
            Cartoon.outline.overrideState = Cartoon.outlineColor.overrideState = Cartoon.outlineThreshold.overrideState = true;
            Cartoon.sharpenIntensity.overrideState = true;
            // Other native color, exposure, blur, vignette and bloom settings still blend normally.
            Volume = Root.AddComponent<Volume>();
            Volume.isGlobal = true;
            Volume.priority = 100;
            Volume.weight = 0;
            Volume.sharedProfile = Profile;
            Root.SetActive(true);
        }
        catch { Dispose(); throw; }
    }

    internal bool BelongsTo(Camera camera) => !Disposed && Camera && camera && Camera.Pointer == camera.Pointer;

    internal void RenderVision() { if (!Disposed) Ripples?.RenderFrame(); }

    internal void Tick(float elapsed, float amount, float motion, bool worldVisuals, float closedEyes = 0)
    {
        if (Disposed || !Camera || Volume is null || !Volume || Colors is null || !Colors
            || Chromatic is null || !Chromatic) return;
        Volume.weight = amount;
        Colors.saturation.value = 56;
        Colors.contrast.value = 28 + 4 * Mathf.Sin(elapsed * 0.16f);
        Colors.postExposure.value = 0.15f;
        Colors.hueShift.value = Mathf.Sin(elapsed * 0.18f) * 24;
        var filter = Color.Lerp(new Color(0.97f, 1, 0.99f), new Color(1, 0.96f, 1),
            0.5f + 0.5f * Mathf.Sin(elapsed * 0.13f));
        var gain = ColorBreathing.Sample(new SurfaceTint(1, 1, 1, 1), 7, elapsed, Strength(LSDPreviewMod.MaterialColorStrength));
        Colors.colorFilter.value = new Color(filter.r * gain.Red, filter.g * gain.Green, filter.b * gain.Blue, filter.a);
        var movement = UpdateComfort(amount > 0);
        if (Breathing is not null && Breathing)
            Breathing.intensity.value = RippleRange.LensBreathing(elapsed, Strength(LSDPreviewMod.RippleStrength), motion, movement);
        Chromatic.intensity.value = 0.09f * motion;
        var outline = Strength(LSDPreviewMod.OutlineStrength);
        if (Cartoon is not null && Cartoon)
        {
            Cartoon.outline.overrideState = Cartoon.sharpenIntensity.overrideState = outline > 0;
            Cartoon.outline.value = outline > 0;
            Cartoon.outlineColor.value = new Color(0.035f, 0.025f, 0.07f, outline * 0.8f);
            Cartoon.outlineThreshold.value = 0.8f;
            Cartoon.sharpenIntensity.value = BaseSharpen + outline * 2;
        }
        if (amount > 0)
        {
            TryLayer(ref Ripples, ref AttemptedRipples, () => new SurfaceRipples(Camera), "ripples");
            if (LSDPreviewMod.PhoneEffects.Value && Strength(LSDPreviewMod.MaterialColorStrength) > 0)
                TryLayer(ref PhoneColors, ref AttemptedColors, () => new PhoneColorBreathing(), "phone colors");
            if (Strength(LSDPreviewMod.AudioStrength) > 0)
                TryLayer(ref Audio, ref AttemptedAudio, () => new PreviewAudio(), "audio");
            if (Strength(LSDPreviewMod.AfterimageStrength) * motion > 0)
                TryLayer(ref Trails, ref AttemptedTrails, () => new Afterimages(Camera), "afterimages");
            if (Strength(LSDPreviewMod.TextBlurStrength) > 0 || Strength(LSDPreviewMod.TextFocusStrength) > 0)
                TryLayer(ref Text, ref AttemptedText, () => new HudTextBlur(), "text");
        }
        Ripples?.Tick(elapsed, amount, Strength(LSDPreviewMod.RippleStrength), motion, movement,
            amount > 0 ? closedEyes : 0, amount * Strength(LSDPreviewMod.ClosedEyeStrength));
        PhoneColors?.Tick(elapsed, LSDPreviewMod.PhoneEffects.Value ? amount * Strength(LSDPreviewMod.MaterialColorStrength) : 0);
        Audio?.Tick(elapsed, amount * Strength(LSDPreviewMod.AudioStrength), closedEyes);
        Trails?.Tick(amount * Strength(LSDPreviewMod.AfterimageStrength) * motion);
        Text?.Tick(elapsed, amount * Strength(LSDPreviewMod.TextBlurStrength), amount * Strength(LSDPreviewMod.TextFocusStrength));
        var hallucination = Strength(LSDPreviewMod.HallucinationStrength);
        if (worldVisuals && amount > 0 && hallucination > 0)
        {
            if (World is null && !AttemptedWorld)
            {
                AttemptedWorld = true;
                try { World = new WorldHallucinations(Camera); }
                catch (Exception error) { ModSupport.WarnOnce("lsd:world", $"LSD character hallucinations unavailable: {error.Message}"); }
            }
            World?.Tick(elapsed, amount, hallucination);
        }
        else World?.Hide();
    }

    private float UpdateComfort(bool active)
    {
        var position = Camera.transform.position;
        var rotation = Camera.transform.rotation;
        var delta = Time.unscaledDeltaTime;
        if (active && TrackedMotion && delta > 0 && delta < 0.25f)
        {
            var travel = Vector3.Distance(position, LastPosition);
            var turn = Quaternion.Angle(rotation, LastRotation);
            if (travel < 18 && turn < 100) Comfort.Step(delta, travel / delta, turn / delta);
        }
        LastPosition = position;
        LastRotation = rotation;
        TrackedMotion = active;
        return Comfort.Factor;
    }

    private static float Strength(MelonLoader.MelonPreferences_Entry<float> setting)
        => TripTimeline.Safe(setting.Value, 0, 0, 1);

    private static void TryLayer<T>(ref T? layer, ref bool attempted, Func<T> create, string name) where T : class
    {
        if (attempted) return;
        attempted = true;
        try { layer = create(); }
        catch (Exception error) { ModSupport.WarnOnce("lsd:" + name, $"LSD {name} layer unavailable: {error.Message}"); }
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        // Disable contribution immediately; Unity destroys objects at the end of the frame.
        if (Volume is not null && Volume) { Volume.weight = 0; Volume.enabled = false; }
        if (Root is not null && Root) Root.SetActive(false);
        DisposeLayer(Ripples, "ripples");
        DisposeLayer(PhoneColors, "phone colors");
        DisposeLayer(Audio, "audio");
        DisposeLayer(Trails, "afterimages");
        DisposeLayer(Text, "text");
        Ripples = null;
        PhoneColors = null;
        Audio = null;
        Trails = null;
        Text = null;
        DisposeLayer(World, "characters");
        World = null;
        if (Root is not null && Root) Object.Destroy(Root);
        if (Profile is not null && Profile)
        {
            foreach (var component in Profile.components) if (component) Object.Destroy(component);
            Object.Destroy(Profile);
        }
        Root = null;
        Profile = null;
    }

    private static void DisposeLayer(IDisposable? layer, string name)
    {
        try { layer?.Dispose(); }
        catch (Exception error) { ModSupport.WarnOnce("lsd:cleanup:" + name, $"LSD {name} cleanup failed: {error.Message}"); }
    }
}
