using Il2CppInterop.Runtime;
using Il2CppScheduleOne;
using Il2CppScheduleOne.PlayerScripts;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(PersonalMods.TVProof.TVProofMod), "Personal TV Proof", "0.1.1", "Axl47")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace PersonalMods.TVProof;

// Disposable local-only experiment. No registered items, network objects or save data.
public sealed class TVProofMod : MelonMod
{
    private MelonPreferences_Entry<string> VideoPath = null!;
    private LocalScreen? Screen;
    private PlaybackCheck? Check;
    private string Status = "";
    private readonly bool CheckRequested = Environment.GetCommandLineArgs().Contains("--tv-poc-check");
    private bool CheckStarted;

    public override void OnInitializeMelon()
    {
        var category = MelonPreferences.CreateCategory("PersonalTVProof");
        VideoPath = category.CreateEntry("VideoPath", @"C:\Users\ortaa\Videos\Moon Landing.mp4",
            description: "Local MP4 for this disposable single-player proof of concept. The original file is read only.");
        LoggerInstance.Msg("TV proof ready. In a loaded save: F7 creates/moves the screen, F6 pauses/resumes, F10 removes it.");
    }

    public override void OnUpdate()
    {
        try
        {
            if (CheckRequested)
            {
                if (!CheckStarted && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Menu")
                {
                    CheckStarted = true;
                    Check = new PlaybackCheck(VideoPath.Value);
                }
                Check?.Tick();
                return;
            }
            var keys = Keyboard.current;
            if (keys?.f10Key.wasPressedThisFrame == true) { Clear(); return; }
            var camera = PlayerCamera.Instance;
            if (!Player.Local || !camera || !camera.Camera) { Clear(); return; }
            // A camera can outlive its save: scene transitions are also handled below.
            Screen?.Tick();
            if (GameInput.IsTyping || camera.ActiveUIElementCount > 0) return;
            if (keys?.f7Key.wasPressedThisFrame == true)
            {
                if (Screen is null) Screen = new LocalScreen(VideoPath.Value, message => Status = message);
                Screen.Place(camera.Camera.transform);
            }
            if (keys?.f6Key.wasPressedThisFrame == true) Screen?.TogglePause();
        }
        catch (Exception error)
        {
            Clear();
            Status = "TV proof: " + error.GetBaseException().Message;
            LoggerInstance.Error(Status);
            if (CheckRequested)
            {
                var output = Environment.GetEnvironmentVariable("TV_POC_OUTPUT");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    File.WriteAllText(Path.Combine(output, "result.txt"), "FAIL\n" + error.GetBaseException());
                }
                Check?.Dispose();
                Application.Quit();
            }
        }
    }

    public override void OnGUI()
    {
        if (Status.Length == 0 || CheckRequested) return;
        GUI.Box(new Rect(16, 150, 650, 48), Status + "\nF7: move screen here    F6: pause/resume    F10: remove");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (!CheckRequested) Clear();
    }

    public override void OnDeinitializeMelon() { Clear(); Check?.Dispose(); }

    private void Clear() { Screen?.Dispose(); Screen = null; Status = ""; }
}

internal sealed class LocalScreen : IDisposable
{
    internal readonly GameObject Root;
    internal readonly VideoPlayer Player;
    internal readonly AudioSource Audio;
    internal readonly RenderTexture VideoTexture;
    private readonly Material FaceMaterial, FrameMaterial;
    private readonly VideoPlayer.ErrorEventHandler ErrorHandler;
    private readonly Action<string> Report;
    private readonly float PreparingAt;
    private bool Started, Disposed, Paused;
    internal string Failure { get; private set; } = "";

    internal LocalScreen(string path, Action<string> report)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Video not found. Set PersonalTVProof.VideoPath in MelonPreferences.cfg.", path);
        Report = report;
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? throw new InvalidOperationException("The game's unlit screen shader is unavailable.");
        Root = new GameObject("Local TV proof - never saved");
        try
        {
        FaceMaterial = new Material(shader);
        FrameMaterial = new Material(shader);
        FrameMaterial.SetColor("_BaseColor", new Color(0.025f, 0.025f, 0.03f, 1));
        FaceMaterial.SetColor("_BaseColor", Color.white);
        VideoTexture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
        if (!VideoTexture.Create()) { Dispose(); throw new InvalidOperationException("Could not allocate the video texture."); }
        var previous = RenderTexture.active;
        try { RenderTexture.active = VideoTexture; GL.Clear(true, true, Color.black); }
        finally { RenderTexture.active = previous; }
        FaceMaterial.SetTexture("_BaseMap", VideoTexture);
        FaceMaterial.mainTexture = VideoTexture;
        var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "TV frame";
        frame.transform.SetParent(Root.transform, false);
        frame.transform.localPosition = new Vector3(0, 0, 0.06f);
        frame.transform.localScale = new Vector3(2.52f, 1.47f, 0.1f);
        frame.GetComponent<MeshRenderer>().sharedMaterial = FrameMaterial;
        // No colliders: this visual experiment must not block navigation or placement.
        Object.Destroy(frame.GetComponent<Collider>());
        var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
        face.name = "TV video surface";
        face.transform.SetParent(Root.transform, false);
        face.transform.localScale = new Vector3(2.4f, 1.35f, 1);
        face.GetComponent<MeshRenderer>().sharedMaterial = FaceMaterial;
        Object.Destroy(face.GetComponent<Collider>());
        Audio = Root.AddComponent<AudioSource>();
        Audio.playOnAwake = false;
        Audio.spatialBlend = 1;
        Audio.rolloffMode = AudioRolloffMode.Linear;
        Audio.minDistance = 2;
        Audio.maxDistance = 20;
        Audio.volume = 0.65f;
        Audio.dopplerLevel = 0;
        Player = Root.AddComponent<VideoPlayer>();
        Player.playOnAwake = false;
        Player.source = VideoSource.Url;
        Player.url = new Uri(path).AbsoluteUri;
        Player.renderMode = VideoRenderMode.RenderTexture;
        Player.targetTexture = VideoTexture;
        Player.aspectRatio = VideoAspectRatio.FitInside;
        Player.isLooping = true;
        Player.waitForFirstFrame = true;
        Player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        Player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        Player.controlledAudioTrackCount = 1;
        Player.EnableAudioTrack(0, true);
        Player.SetTargetAudioSource(0, Audio);
        ErrorHandler = DelegateSupport.ConvertDelegate<VideoPlayer.ErrorEventHandler>(
            (Action<VideoPlayer, string>)((_, message) => Fail(message)))!;
        Player.errorReceived += ErrorHandler;
        PreparingAt = Time.realtimeSinceStartup;
        Report("TV proof: preparing " + Path.GetFileName(path));
        MelonLogger.Msg("TV_POC preparing local video: " + path);
        Player.Prepare();
        }
        catch { Dispose(); throw; }
    }

    internal void Place(Transform view)
    {
        var forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
        Root.transform.SetPositionAndRotation(view.position + forward * 3, Quaternion.LookRotation(forward, Vector3.up));
    }

    internal void Tick()
    {
        if (Disposed || Started || Failure.Length > 0) return;
        if (!NativeVideoState.IsPrepared(Player))
        {
            if (Time.realtimeSinceStartup - PreparingAt > 30) Fail("Video preparation timed out.");
            return;
        }
        // Prepare supplies the actual source dimensions. Resize before playback,
        // preserving this texture object so the face material stays bound.
        if (Player.width > 0 && Player.height > 0)
        {
            var maximum = Math.Min(4096, SystemInfo.maxTextureSize);
            var scale = Math.Min(1.0, maximum / (double)Math.Max(Player.width, Player.height));
            var width = Math.Max(1, (int)Math.Round(Player.width * scale));
            var height = Math.Max(1, (int)Math.Round(Player.height * scale));
            Player.targetTexture = null;
            VideoTexture.Release();
            VideoTexture.width = width;
            VideoTexture.height = height;
            if (!VideoTexture.Create()) { Fail("Could not allocate the source-resolution video texture."); return; }
            Player.targetTexture = VideoTexture;
        }
        Started = true;
        Paused = false;
        Player.Play();
        Report("TV proof: playing " + Path.GetFileName(new Uri(Player.url).LocalPath));
        MelonLogger.Msg($"TV_POC prepared {Player.width}x{Player.height}, render texture={VideoTexture.width}x{VideoTexture.height}, {Player.frameRate:F2} fps, {Player.length:F2}s, audio tracks={Player.audioTrackCount}, seekable={NativeVideoState.CanSeek(Player)}.");
    }

    internal void TogglePause()
    {
        if (!Started || Failure.Length > 0) return;
        // Keep the requested state locally: rapid toggles must not depend on
        // asynchronous decoder state or the generated bool return marshalling.
        if (Paused) { Player.Play(); Paused = false; Report("TV proof: playing"); }
        else { Player.Pause(); Paused = true; Report("TV proof: paused"); }
        MelonLogger.Msg($"TV_POC requested {(Paused ? "pause" : "resume")}; generated isPlaying={Player.isPlaying}, native isPlaying={NativeVideoState.IsPlaying(Player)}, native isPaused={NativeVideoState.IsPaused(Player)}, time={Player.time:F3}.");
    }

    private void Fail(string message)
    {
        Failure = message;
        Report("TV proof error: " + message);
        MelonLogger.Error("TV_POC decoder error: " + message);
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        if (Player) { if (ErrorHandler is not null) Player.errorReceived -= ErrorHandler; Player.Stop(); }
        if (Audio) Audio.Stop();
        if (Root) Object.Destroy(Root);
        if (FaceMaterial) Object.Destroy(FaceMaterial);
        if (FrameMaterial) Object.Destroy(FrameMaterial);
        if (VideoTexture) { VideoTexture.Release(); Object.Destroy(VideoTexture); }
    }
}
