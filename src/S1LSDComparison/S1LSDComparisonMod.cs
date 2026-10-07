using System.Collections;
using System.Reflection;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using MelonLoader;
using S1LSD.Core;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(PersonalMods.S1LSDComparison.S1LSDComparisonMod), "Disposable S1LSD Visual Comparison", "0.1.0", "Axl47; S1LSD Contributors (MIT effects)")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PersonalMods.S1LSDComparison;

public sealed class S1LSDComparisonMod : MelonMod
{
    private const string UIKey = "S1LSDComparison";
    private ComparisonSession Session = null!;
    private ComparisonRenderer? Renderer;
    private ComparisonVolume? Volume;
    private BaselineLease? Baseline;
    private Camera? Camera;
    private PlayerCamera? PanelCamera;
    private object? FrameLoop;
    private ShaderTargets Targets;
    private bool PanelOpen, PreviousLook, PreviousTyping, Tracked;
    private Quaternion LastRotation;
    private float Dwell, Gaze = 1, Saccade;
    private int PendingDose;
    private string Status = "Choose a tier. Our current effect is suspended only during comparison.";
    private readonly bool RequestedCheck = Environment.GetCommandLineArgs().Contains("--s1lsd-comparison-check");
    private bool Checked;
    private ComparisonCheck? Diagnostic;

    public override void OnInitializeMelon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Comparison.Defaults.json")!;
        using var reader = new StreamReader(stream);
        Session = ComparisonSession.FromJson(reader.ReadToEnd());
        LoggerInstance.Msg("Disposable S1LSD comparison ready: F6 menu; F10 stops. Visuals only. No recipes, items, saves, startup shaders or audio patches.");
    }

    public override void OnUpdate()
    {
        try {
            var keyboard = Keyboard.current;
            if (keyboard?.f10Key.wasPressedThisFrame == true) { Stop(); return; }
            if (RequestedCheck && !Checked && Time.realtimeSinceStartup > 5
                && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Menu") {
                Checked = true;
                var camera = UnityEngine.Camera.allCameras.FirstOrDefault(c => c && c.enabled && !c.targetTexture && c.cameraType == CameraType.Game);
                if (!camera) throw new InvalidOperationException("Menu check could not find a rendered camera.");
                Baseline = new BaselineLease();
                Diagnostic = new ComparisonCheck(Session, camera!);
                FrameLoop = MelonCoroutines.Start(RenderCheck());
            }
            if (Diagnostic is not null) return;
            var loading = LoadManager.Instance;
            if (!loading || !loading.IsGameLoaded || loading.IsLoading || !Player.Local || !Player.Local.PlayerLoaded) {
                if (PanelOpen || Session.Active || Renderer is not null) Stop();
                return;
            }
            var playerCamera = PlayerCamera.Instance;
            if (!playerCamera || !playerCamera.Camera) { if (Session.Active) Stop(); return; }
            if (keyboard?.f6Key.wasPressedThisFrame == true) {
                if (PanelOpen) ClosePanel();
                else if (!GameInput.IsTyping && playerCamera.ActiveUIElementCount == 0) OpenPanel(playerCamera);
            }
            if (PanelOpen && keyboard?.escapeKey.wasPressedThisFrame == true) ClosePanel();
            if (PendingDose < 0) { Stop(); return; }
            if (PendingDose != 0) { var dose = PendingDose; PendingDose = 0; Start(dose, playerCamera.Camera); }
            if (!Session.Active) return;
            var currentCamera = playerCamera.Camera;
            if (!currentCamera || !currentCamera.enabled || !currentCamera.gameObject.activeInHierarchy) {
                currentCamera = UnityEngine.Camera.allCameras.FirstOrDefault(c => c && c.enabled && c.gameObject.activeInHierarchy
                    && c != playerCamera.OverlayCamera && !c.targetTexture && c.cameraType == CameraType.Game);
            }
            if (!currentCamera) return;
            Camera = currentCamera;
            var dt = Mathf.Clamp(Time.unscaledDeltaTime, 0, 0.1f);
            var rotation = currentCamera!.transform.rotation;
            var speed = Tracked && dt > 0 ? Quaternion.Angle(rotation, LastRotation) / dt : 0;
            LastRotation = rotation; Tracked = true;
            Dwell = speed < 5 ? Dwell + dt : 0;
            Gaze = Mathf.MoveTowards(Gaze, Dwell >= 2 ? 1.25f : 1, dt * 0.2f);
            Saccade = Mathf.MoveTowards(Saccade, speed > 120 ? 1 : 0, dt * 3);
            Session.Tick(dt);
            if (!Session.Active) { Stop(); return; }
            var input = Session.Inputs(Gaze, Saccade);
            Targets = Session.Shader(input); var grade = Session.Grade(input);
            Volume?.Tick(grade);
            if (Renderer is not null && FrameLoop is null) FrameLoop = MelonCoroutines.Start(RenderFrames());
        } catch (Exception error) { Failed(error); }
    }

    private void Start(int dose, Camera camera)
    {
        Stop();
        try {
            Baseline = new BaselineLease();
            Renderer = new ComparisonRenderer(); Volume = new ComparisonVolume(camera);
            Camera = camera; Tracked = false; Gaze = 1; Dwell = Saccade = 0;
            Session.Start(dose);
            Status = $"{dose} tier / {(Session.Classic ? "Classic" : "Subtle")} / {(Session.HoldPeak ? "held peak" : "full timeline")}";
            LoggerInstance.Msg($"S1LSD comparison started: {Status}. Original shader, neutral setting, no tolerance; original audio omitted.");
        } catch { Stop(); throw; }
    }

    private IEnumerator RenderFrames()
    {
        var completed = new WaitForEndOfFrame();
        try {
            while (Renderer is not null && Session.Active) {
                yield return completed;
                try {
                    if (Camera && Renderer is not null && Session.Active) {
                        Renderer.Render(Camera!, Targets, Session.VisualTime);
                        if (Renderer.Frames == 1) LoggerInstance.Msg("S1LSD comparison completed-frame renderer is drawing.");
                    }
                } catch (Exception error) { Failed(error); }
            }
        } finally { FrameLoop = null; }
    }

    private IEnumerator RenderCheck()
    {
        var completed = new WaitForEndOfFrame();
        try {
            while (Diagnostic is not null && !Diagnostic.Complete) {
                yield return completed;
                try { Diagnostic.RenderFrame(); }
                catch (Exception error) { Failed(error); }
            }
        } finally {
            Diagnostic?.Dispose(); Diagnostic = null;
            Baseline?.Dispose(); Baseline = null; FrameLoop = null;
        }
    }

    private void OpenPanel(PlayerCamera camera)
    {
        PanelCamera = camera; PreviousLook = camera.CanLook; PreviousTyping = GameInput.IsTyping;
        camera.AddActiveUIElement(UIKey); camera.SetCanLook(false); camera.FreeMouse(true);
        GameInput.IsTyping = true; PanelOpen = true;
    }

    private void ClosePanel()
    {
        if (!PanelOpen) return;
        PanelOpen = false;
        if (PanelCamera) {
            PanelCamera!.RemoveActiveUIElement(UIKey);
            PanelCamera.SetCanLook(PreviousLook);
            if (PanelCamera.ActiveUIElementCount == 0) PanelCamera.LockMouse(true);
        }
        GameInput.IsTyping = PreviousTyping; PanelCamera = null;
    }

    public override void OnGUI()
    {
        if (!PanelOpen || Session is null) return;
        var width = Mathf.Min(560, Screen.width - 24);
        GUILayout.BeginArea(new Rect((Screen.width - width) / 2, 90, width, 405), GUI.skin.box);
        GUILayout.Label("S1LSD 0.11.0 — disposable visual comparison");
        GUILayout.Label("Original dose labels: 70 / 140 / 280 / 360 µg");
        GUILayout.Label("Visuals only: texture crawl, trails, breathing, colors and geometry.");
        GUILayout.Space(8);
        var classic = GUILayout.Toolbar(Session.Classic ? 0 : 1, new[] { "Classic", "Subtle" }) == 0;
        var hold = GUILayout.Toolbar(Session.HoldPeak ? 0 : 1, new[] { "Hold peak", "Full timeline" }) == 0;
        if (classic != Session.Classic || hold != Session.HoldPeak) {
            Session.Classic = classic; Session.HoldPeak = hold;
            if (Session.Active) PendingDose = Session.Effect.Dose;
        }
        GUILayout.Space(8); GUILayout.BeginHorizontal();
        foreach (var dose in ComparisonSession.Doses) if (GUILayout.Button($"Preview {dose}")) PendingDose = dose;
        GUILayout.EndHorizontal(); GUILayout.Space(8);
        GUILayout.Label("Full timeline: 3.5 / 5.5 / 8.5 / 11 minutes respectively.");
        GUILayout.Label(Status);
        if (Session.Active) GUILayout.Label($"Phase: {Session.Effect.Phase} · rendered frames: {Renderer?.Frames ?? 0}");
        GUILayout.Space(8);
        if (GUILayout.Button("Stop comparison — restore personal preview")) PendingDose = -1;
        if (GUILayout.Button("Close panel (F6)")) ClosePanel();
        GUILayout.Label("F10 clears immediately, including while the phone is open.");
        GUILayout.EndArea();
    }

    private void Stop()
    {
        Diagnostic?.Dispose(); Diagnostic = null;
        ClosePanel(); PendingDose = 0; Session?.Stop();
        var renderer = Renderer; Renderer = null; renderer?.Dispose();
        var volume = Volume; Volume = null; volume?.Dispose();
        var baseline = Baseline; Baseline = null; baseline?.Dispose(); Camera = null;
        Status = "Comparison stopped. Your personal preview is available again with F8.";
    }

    private void Failed(Exception error)
    {
        Stop(); Status = $"Comparison stopped: {error.GetBaseException().Message}";
        LoggerInstance.Error($"{Status}\n{error.GetBaseException()}");
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName) { if (PanelOpen || Renderer is not null) Stop(); }
    public override void OnApplicationQuit() => Stop();
    public override void OnDeinitializeMelon() => Stop();
}
