using System.Collections;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[assembly: MelonInfo(typeof(PersonalMods.LSDPreview.LSDPreviewMod), "Personal LSD Visual Preview", "0.10.0", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PersonalMods.LSDPreview;

public sealed class LSDPreviewMod : MelonMod
{
    private const float DefaultIntensity = 0.60f, DefaultMotion = 0.50f, DefaultRipple = RippleRange.DefaultStrength,
        DefaultAfterimage = 0.625f, DefaultTextBlur = 0.375f;
    internal static MelonPreferences_Entry<bool> Enabled = null!, WorldVisuals = null!, PhoneEffects = null!;
    internal static MelonPreferences_Entry<string> GhostModelNPC = null!;
    internal static MelonPreferences_Entry<float> Intensity = null!, MotionStrength = null!, DurationSeconds = null!,
        RippleStrength = null!, AfterimageStrength = null!, OutlineStrength = null!, TextBlurStrength = null!,
        MaterialColorStrength = null!, HallucinationStrength = null!, AudioStrength = null!, CenterClarityDegrees = null!;
    private readonly TripTimeline Timeline = new();
    private readonly bool RequestedShaderCheck = Environment.GetCommandLineArgs().Contains("--lsd-render-check"),
        RequestedAudioCheck = Environment.GetCommandLineArgs().Contains("--lsd-audio-check");
    private AudioCheck? CheckAudio;
    private FrameCheck? CheckFrames;
    private object? FrameLoopToken;
    private bool CheckedAudio;
    private PreviewEffects? Effects;
    private IntPtr PlayerPointer, ProductPointer;
    private bool Suspended, LoggedMainLoop, CheckedShader;

    public override void OnInitializeMelon()
    {
        var config = MelonPreferences.CreateCategory("PersonalLSDPreview");
        Enabled = ModSupport.Entry(config, "Enabled", true, "Allow the manual F8 preview. Effects never start automatically.");
        WorldVisuals = ModSupport.Entry(config, "WorldVisuals", true, "Show faded hallucinations built from the game's character meshes.");
        PhoneEffects = ModSupport.Entry(config, "PhoneEffects", true, "Gentle color breathing and soft colored text fringes on the open phone.");
        Intensity = ModSupport.Entry(config, "Intensity", DefaultIntensity, "Overall artistic effect strength from 0 to 1.");
        MotionStrength = ModSupport.Entry(config, "MotionStrength", DefaultMotion, "Breathing, flowing ripples, color separation and afterimages from 0 to 1; 0 disables them.");
        DurationSeconds = ModSupport.Entry(config, "DurationSeconds", TripTimeline.DefaultDuration, "Compressed preview length in seconds, from 20 to 600.");
        RippleStrength = ModSupport.Entry(config, "RippleStrength", DefaultRipple, "Completed-frame flow and breathing from 0 to 1; default 0.30, default pixel bound 5.4, absolute bound 18. Also follows MotionStrength.");
        CenterClarityDegrees = ModSupport.Entry(config, "CenterClarityDegrees", RippleRange.DefaultCenterDegrees, "Reserved for the disabled peripheral experiment. The default completed-frame path has no gaze mask.");
        AfterimageStrength = ModSupport.Entry(config, "AfterimageStrength", DefaultAfterimage, "Brief delayed image trails from 0 to 1; also follows MotionStrength.");
        MaterialColorStrength = ModSupport.Entry(config, "MaterialColorStrength", 0.45f, "Continuous slow RGB color breathing across the scene and phone, from 0 to 1. No material discovery or texture copies.");
        HallucinationStrength = ModSupport.Entry(config, "HallucinationStrength", 0.55f, "Opacity of faded character hallucinations, from 0 to 1; 0 hides them.");
        AudioStrength = ModSupport.Entry(config, "AudioStrength", 0.35f, "Direct 220ms echo on original native music, ambience and effects buses, from 0 to 1. Zero restores the original routing.");
        OutlineStrength = ModSupport.Entry(config, "OutlineStrength", 0.7f, "Defined cartoon-like edges and sharpening from 0 to 1.");
        TextBlurStrength = ModSupport.Entry(config, "TextBlurStrength", DefaultTextBlur, "Soft/indistinct HUD and dialogue text from 0 to 1; also colors phone text fringes when PhoneEffects is enabled.");
        GhostModelNPC = ModSupport.Entry(config, "GhostModelNPC", "", "NPC ID or full name used for hallucination meshes. Empty selects a nearby loaded character.");
        UpgradeDefaults(config);
        LoggerInstance.Msg("LSD visual experiment ready: F8 starts/fades out; F9 clears immediately. No product is registered.");
        LoggerInstance.Msg("LSD startup audio hooks removed. Audio observation begins only during an active preview.");
    }

    private void UpgradeDefaults(MelonPreferences_Category config)
    {
        var revision = config.CreateEntry("DefaultsRevision", 0, description: "Internal marker for one-time preview default upgrades.", is_hidden: true);
        if (revision.Value >= 4) return;
        // A one-time upgrade of untouched defaults, leaving custom values intact.
        // The marker also lets a later edit deliberately restore the old strength.
        var changed = 0;
        if (revision.Value < 1)
            changed += Upgrade(Intensity, 0.85f) + Upgrade(MotionStrength, 0.65f)
                + Upgrade(RippleStrength, 0.75f) + Upgrade(AfterimageStrength, 0.65f);
        if (revision.Value < 2)
            changed += Upgrade(AfterimageStrength, 0.50f) + Upgrade(TextBlurStrength, 0.25f) + Upgrade(DurationSeconds, 90f);
        if (revision.Value < 3) changed += Upgrade(RippleStrength, 0.50f);
        if (revision.Value < 4) changed += Upgrade(CenterClarityDegrees, 12f);
        revision.Value = 4;
        config.SaveToFile(false);
        if (changed > 0) LoggerInstance.Msg("Applied updated LSD preview defaults; custom settings were preserved.");
    }

    private static int Upgrade(MelonPreferences_Entry<float> entry, float previousDefault)
    {
        if (entry.Value != previousDefault) return 0;
        entry.ResetToDefault();
        return 1;
    }

    public override void OnUpdate()
    {
        try
        {
            if (!LoggedMainLoop)
            {
                LoggedMainLoop = true;
                LoggerInstance.Msg("LSD preview main-loop heartbeat: update callbacks are running without audio detours.");
            }
            // The emergency reset also works while a menu is open or input is blocked.
            var keyboard = Keyboard.current;
            if (!CheckedShader && RequestedShaderCheck
                && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Menu")
            {
                CheckedShader = true;
                ShaderCheck.Run();
                CheckFrames = new FrameCheck();
            }
            if (CheckFrames is not null && CheckFrames.Tick()) { CheckFrames.Dispose(); CheckFrames = null; }
            EnsureFrameLoop();
            if (RequestedAudioCheck && CheckFrames is null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Menu")
            {
                if (!CheckedAudio)
                {
                    CheckedAudio = true;
                    CheckAudio = new AudioCheck();
                }
                if (CheckAudio is not null)
                {
                    if (CheckAudio.Tick())
                    {
                        CheckAudio.Dispose();
                        CheckAudio = null;
                    }
                }
            }
            if (keyboard is not null && keyboard.f9Key.wasPressedThisFrame) { Cleanup(); DisposeChecks(); return; }
            if (!Enabled.Value || !TryContext(out var player, out var playerCamera, out var camera)) { Cleanup(); return; }
            if (Effects is not null && (PlayerPointer != player.Pointer
                || ProductPointer != (player.ConsumedProduct?.Pointer ?? IntPtr.Zero)))
            { Cleanup(); return; }

            // Rebind a replacement camera without discarding the current trip.
            if (Effects is not null && !Effects.BelongsTo(camera))
            {
                Effects.Dispose();
                Effects = null;
                Effects = new PreviewEffects(camera);
            }

            var paused = Time.timeScale <= 0 || (PauseMenu.Instance && PauseMenu.Instance.IsPaused);
            var menuOpen = (Phone.Instance && Phone.Instance.IsOpen) || (DialogueCanvas.Instance && DialogueCanvas.Instance.IsOpen)
                || Cursor.visible;
            var riding = player.IsInVehicle || player.ActiveSkateboard;
            Suspended = PreviewVisibility.Suspend(paused, Application.isFocused, GameInput.IsTyping, menuOpen,
                riding, playerCamera.CanLook, playerCamera.ActiveUIElementCount > 0);
            if (keyboard is not null && keyboard.f8Key.wasPressedThisFrame && !Suspended)
            {
                if (Timeline.Active) Timeline.RequestStop();
                else
                {
                    // Each preview owns its visual objects and profile.
                    Effects = new PreviewEffects(camera);
                    PlayerPointer = player.Pointer;
                    ProductPointer = player.ConsumedProduct?.Pointer ?? IntPtr.Zero;
                    Timeline.Start(DurationSeconds.Value);
                    LoggerInstance.Msg("LSD visual preview started. F9 clears it immediately.");
                    EnsureFrameLoop();
                }
            }
            Timeline.Step(Time.unscaledDeltaTime, Suspended);
            if (Effects is not null && !Timeline.Active) Cleanup();
        }
        catch (Exception error)
        {
            Cleanup();
            ModSupport.WarnOnce("lsd:preview", $"LSD preview could not continue: {error.Message}");
        }
    }

    public override void OnLateUpdate()
    {
        if (Effects is null) return;
        try
        {
            Effects.Tick(Timeline.Elapsed, Suspended ? 0 : Timeline.Envelope * TripTimeline.Safe(Intensity.Value, DefaultIntensity, 0, 1),
                TripTimeline.Safe(MotionStrength.Value, DefaultMotion, 0, 1), WorldVisuals.Value);
        }
        catch (Exception error)
        {
            Cleanup();
            ModSupport.WarnOnce("lsd:render", $"LSD preview was cleared after a visual update failed: {error.Message}");
        }
    }

    private void EnsureFrameLoop()
    {
        if (FrameLoopToken is null && (Effects is not null || CheckFrames is not null))
            FrameLoopToken = MelonCoroutines.Start(RenderCompletedFrames());
    }

    private IEnumerator RenderCompletedFrames()
    {
        var completed = new WaitForEndOfFrame();
        try
        {
            while (Effects is not null || CheckFrames is not null)
            {
                yield return completed;
                try { Effects?.RenderVision(); CheckFrames?.RenderFrame(); }
                catch (Exception error)
                {
                    Cleanup(); DisposeChecks();
                    ModSupport.WarnOnce("lsd:frame", $"LSD completed-frame draw failed: {error.GetBaseException()}");
                }
            }
        }
        finally { FrameLoopToken = null; }
    }

    private static bool TryContext(out Player player, out PlayerCamera playerCamera, out Camera camera)
    {
        player = Player.Local;
        playerCamera = PlayerCamera.Instance;
        camera = playerCamera ? playerCamera.Camera : null!;
        var loading = LoadManager.Instance;
        // Prefer the known world camera. A vehicle can temporarily substitute a
        // different base camera; ignore UI/overlay cameras during that handoff.
        if (playerCamera && (!camera || !camera.enabled || !camera.gameObject.activeInHierarchy))
        {
            foreach (var candidate in Camera.allCameras)
            {
                if (!candidate || !candidate.enabled || !candidate.gameObject.activeInHierarchy || candidate == playerCamera.OverlayCamera
                    || candidate.targetTexture || candidate.cameraType != CameraType.Game) continue;
                var data = candidate.GetComponent<UniversalAdditionalCameraData>();
                if (data && data.renderType == CameraRenderType.Base && data.renderPostProcessing)
                { camera = candidate; break; }
            }
        }
        return loading && loading.IsGameLoaded && !loading.IsLoading && player && player.PlayerLoaded
            && !player.IsUnconscious && player.Health && player.Health.IsAlive
            && playerCamera && camera && camera.gameObject.activeInHierarchy;
    }

    private void Cleanup()
    {
        Timeline.Reset();
        PlayerPointer = ProductPointer = IntPtr.Zero;
        var effects = Effects;
        Effects = null;
        effects?.Dispose();
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        => LoggerInstance.Msg($"LSD preview observed scene loaded: {sceneName}.");
    private void DisposeChecks() { CheckAudio?.Dispose(); CheckAudio = null; CheckFrames?.Dispose(); CheckFrames = null; }
    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (sceneName == "Main") Cleanup();
        if (sceneName == "Menu") DisposeChecks();
    }
    public override void OnApplicationQuit() { Cleanup(); DisposeChecks(); }
    public override void OnDeinitializeMelon() { Cleanup(); DisposeChecks(); }
}
