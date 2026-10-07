using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.TVProof;

// Opt-in check in the menu. Never loads, changes, or saves a player save.
internal sealed class PlaybackCheck : IDisposable
{
    private readonly LocalScreen Screen;
    private readonly GameObject CameraRoot;
    private readonly Camera View;
    private readonly RenderTexture WorldTexture;
    private readonly bool PreviousBackground;
    private readonly string OutputDirectory;
    private Color32[]? FirstPixels;
    private Color32[]? FirstWorldPixels;
    private int Phase;
    private float Started = Time.realtimeSinceStartup, PhaseAt;
    private double PausedTime;
    private bool Finished;

    internal PlaybackCheck(string path)
    {
        PreviousBackground = Application.runInBackground;
        Application.runInBackground = true;
        OutputDirectory = Path.GetFullPath(Environment.GetEnvironmentVariable("TV_POC_OUTPUT")
            ?? Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "PersonalTVProof", "Check"));
        Directory.CreateDirectory(OutputDirectory);
        Screen = new LocalScreen(path, MelonLogger.Msg);
        CameraRoot = new GameObject("TV proof check camera");
        View = CameraRoot.AddComponent<Camera>();
        View.transform.position = new Vector3(10000, 10000, 10000);
        View.transform.rotation = Quaternion.identity;
        View.clearFlags = CameraClearFlags.SolidColor;
        View.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
        View.fieldOfView = 40;
        View.nearClipPlane = 0.1f;
        View.farClipPlane = 10;
        View.cullingMask = 1 << 30;
        WorldTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        if (!WorldTexture.Create()) throw new InvalidOperationException("Cannot allocate the screen camera texture.");
        View.targetTexture = WorldTexture;
        Screen.Place(View.transform);
        foreach (var transform in Screen.Root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 30;
        MelonLogger.Msg("TV_POC_CHECK started; offscreen world camera and real native decoder.");
    }

    internal void Tick()
    {
        if (Finished) return;
        try
        {
            Screen.Tick();
            if (Screen.Failure.Length > 0) throw new InvalidOperationException(Screen.Failure);
            if (Time.realtimeSinceStartup - Started > 50) throw new TimeoutException("The native playback check timed out.");
            var player = Screen.Player;
            switch (Phase)
            {
                case 0 when NativeVideoState.IsPlaying(player) && player.time > 1:
                    if (player.width <= 4096 && player.height <= 4096 &&
                        (Screen.VideoTexture.width != player.width || Screen.VideoTexture.height != player.height))
                        throw new InvalidOperationException("The video texture does not preserve the source resolution.");
                    FirstPixels = Read(Screen.VideoTexture, "decoded-frame.png");
                    if (FirstPixels.Count(c => c.r > 10 || c.g > 10 || c.b > 10) < 1000)
                        throw new InvalidOperationException("The decoded frame contains no visible image.");
                    Phase = 1; PhaseAt = Time.realtimeSinceStartup;
                    break;
                case 1 when Time.realtimeSinceStartup - PhaseAt > 1.5f:
                    var pixels = Read(Screen.VideoTexture, "decoded-frame-later.png");
                    var changed = pixels.Where((c, i) => Different(c, FirstPixels![i])).Count();
                    if (changed < 100) throw new InvalidOperationException("The decoder did not produce changing video frames.");
                    FirstWorldPixels = Read(WorldTexture, "screen-in-world.png");
                    var background = FirstWorldPixels[0];
                    var visible = FirstWorldPixels.Count(c => Different(c, background));
                    if (visible < 10000) throw new InvalidOperationException("The world camera did not render the screen.");
                    MelonLogger.Msg($"TV_POC_CHECK decoded frames change: {changed} pixels; screen world capture saved; audio tracks={player.audioTrackCount}, source playing={Screen.Audio.isPlaying}.");
                    Screen.TogglePause(); Phase = 2; PhaseAt = Time.realtimeSinceStartup;
                    break;
                case 2 when Time.realtimeSinceStartup - PhaseAt > 0.5f:
                    if (NativeVideoState.IsPlaying(player) || !NativeVideoState.IsPaused(player)) throw new InvalidOperationException("Pause failed.");
                    PausedTime = player.time; Phase = 3; PhaseAt = Time.realtimeSinceStartup;
                    break;
                case 3 when Time.realtimeSinceStartup - PhaseAt > 0.7f:
                    if (Math.Abs(player.time - PausedTime) > 0.1) throw new InvalidOperationException("Paused playback continued advancing.");
                    Screen.TogglePause(); Phase = 4; PhaseAt = Time.realtimeSinceStartup;
                    break;
                case 4 when Time.realtimeSinceStartup - PhaseAt > 1:
                    if (!NativeVideoState.IsPlaying(player) || NativeVideoState.IsPaused(player) || player.time <= PausedTime + 0.2)
                        throw new InvalidOperationException("Resume failed to advance playback from its paused position.");
                    // Exercise a second complete toggle cycle, then seek separately.
                    Screen.TogglePause(); Phase = 5; PhaseAt = Time.realtimeSinceStartup;
                    break;
                case 5 when Time.realtimeSinceStartup - PhaseAt > 0.5f:
                    if (NativeVideoState.IsPlaying(player) || !NativeVideoState.IsPaused(player)) throw new InvalidOperationException("Second pause failed.");
                    PausedTime = player.time;
                    Screen.TogglePause(); Phase = 6; PhaseAt = Time.realtimeSinceStartup;
                    break;
                case 6 when Time.realtimeSinceStartup - PhaseAt > 1:
                    if (!NativeVideoState.IsPlaying(player) || player.time <= PausedTime + 0.2) throw new InvalidOperationException("Second resume failed.");
                    if (!NativeVideoState.CanSeek(player)) throw new InvalidOperationException("The local MP4 is not seekable.");
                    player.time = 10; Phase = 7; PhaseAt = Time.realtimeSinceStartup;
                    break;
                case 7 when NativeVideoState.IsPlaying(player) && player.time >= 10.2 && Time.realtimeSinceStartup - PhaseAt > 1:
                    var laterWorld = Read(WorldTexture, "screen-after-seek.png");
                    var worldChanges = laterWorld.Where((c, i) => Different(c, FirstWorldPixels![i])).Count();
                    if (worldChanges < 1000) throw new InvalidOperationException("Video frames change, but the rendered world screen stays unchanged.");
                    MelonLogger.Msg($"TV_POC_CHECK rendered screen changes: {worldChanges} pixels after seek.");
                    Finish(true, $"Source {player.width}x{player.height}, texture {Screen.VideoTexture.width}x{Screen.VideoTexture.height}. Native local MP4 decoding, changing frames, world screen, two pause/resume cycles and seek passed. Audible sound and loaded-save placement still need a human playtest.");
                    break;
            }
        }
        catch (Exception error) { Finish(false, error.GetBaseException().ToString()); }
    }

    private static bool Different(Color32 a, Color32 b) =>
        Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 6;

    private Color32[] Read(RenderTexture target, string filename)
    {
        var previous = RenderTexture.active;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            texture.Apply(false, false);
            File.WriteAllBytes(Path.Combine(OutputDirectory, filename), ImageConversion.EncodeToPNG(texture).ToArray());
            return texture.GetPixels32().ToArray();
        }
        finally { RenderTexture.active = previous; Object.Destroy(texture); }
    }

    private void Finish(bool success, string detail)
    {
        File.WriteAllText(Path.Combine(OutputDirectory, "result.txt"), (success ? "PASS\n" : "FAIL\n") + detail);
        if (success) MelonLogger.Msg("TV_POC_CHECK PASS " + detail);
        else MelonLogger.Error("TV_POC_CHECK FAIL " + detail);
        Dispose();
        Application.Quit();
    }

    public void Dispose()
    {
        if (Finished) return;
        Finished = true;
        Screen.Dispose();
        if (View) View.targetTexture = null;
        if (CameraRoot) Object.Destroy(CameraRoot);
        if (WorldTexture) { WorldTexture.Release(); Object.Destroy(WorldTexture); }
        Application.runInBackground = PreviousBackground;
    }
}
