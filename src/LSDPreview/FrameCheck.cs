using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Explicit menu-only check of the REAL completed-frame hook. Compare its
// input capture to the screen after it draws, including zero-strength identity.
internal sealed class FrameCheck : IDisposable
{
    private SurfaceRipples? Flow;
    private RenderTexture? After;
    private Texture2D? Readback;
    private int Phase, Frames;
    private float Started = Time.realtimeSinceStartup;
    private bool Disposed;

    internal bool Tick()
    {
        if (Disposed) return true;
        if (Time.realtimeSinceStartup - Started > 8)
        {
            ModSupport.WarnOnce("lsd:frame-check:timeout", "The completed-frame diagnostic did not finish.");
            Dispose(); return true;
        }
        if (Time.realtimeSinceStartup - Started < 2) return false;
        if (Flow is null)
        {
            var camera = Camera.allCameras.FirstOrDefault(c => c && c.enabled && !c.targetTexture && c.cameraType == CameraType.Game);
            if (!camera) return false;
            MelonLoader.MelonLogger.Msg($"LSD frame camera selection: '{camera!.name}', active {camera.gameObject.activeInHierarchy}, enabled {camera.enabled}, focus {Application.isFocused}, background {Application.runInBackground}, pipeline '{UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name}'.");
            Flow = new SurfaceRipples(camera!, true);
        }
        Flow.Tick(2.4f, Phase == 0 ? 0 : 0.6f, RippleRange.DefaultStrength, 0.5f, 1);
        return false;
    }

    internal void RenderFrame()
    {
        if (Disposed || Flow is null) return;
        Flow.RenderFrame();
        if (Flow.LastCapture is not { } before || !before || ++Frames < 3) return;
        try
        {
            if (!Readback)
            {
                Readback = new Texture2D(before.width, before.height, TextureFormat.RGBA32, false);
                After = new RenderTexture(before.width, before.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                if (!After.Create()) throw new InvalidOperationException("The final-frame comparison capture could not be created.");
            }
            // Use identical capture APIs for both images. Direct screen
            // ReadPixels has a different origin and hid the previous flip.
            ScreenCapture.CaptureScreenshotIntoRenderTexture(After!);
            var previous = RenderTexture.active;
            Color32[] Read(RenderTexture target)
            {
                RenderTexture.active = target;
                Readback!.ReadPixels(new Rect(0, 0, before.width, before.height), 0, 0, false);
                Readback.Apply(false, false);
                return Readback.GetPixels32().ToArray();
            }
            Color32[] first, second;
            try { first = Read(before); second = Read(After!); }
            finally { RenderTexture.active = previous; }
            var changed = 0;
            for (var i = 0; i < first.Length; i++)
                if (Math.Abs(first[i].r - second[i].r) + Math.Abs(first[i].g - second[i].g) + Math.Abs(first[i].b - second[i].b) > 3) changed++;
            if (Phase == 0)
            {
                var visible = first.Count(c => c.r > 8 || c.g > 8 || c.b > 8);
                MelonLoader.MelonLogger.Msg($"LSD completed-frame capture: {visible}/{first.Length} nonblack pixels.");
                if (visible < 1000) throw new InvalidOperationException("The completed-frame input contains no visible screen image.");
                MelonLoader.MelonLogger.Msg($"LSD completed-frame identity check: {changed}/{first.Length} changed pixels at zero displacement.");
                if (changed > Math.Max(64, visible / 1000)) throw new InvalidOperationException("The completed-frame pass changed orientation or color at zero strength.");
                Phase = 1; Frames = 0;
            }
            else
            {
                if (changed < 100) throw new InvalidOperationException("The completed-frame pass did not alter the final screen image.");
                MelonLoader.MelonLogger.Msg($"LSD FINAL-FRAME flow check PASSED: {changed}/{first.Length} final screen pixels changed at default strength; no gaze mask. This verifies the live frame hook, not just an offscreen shader.");
                Dispose();
            }
        }
        catch (Exception error) { ModSupport.WarnOnce("lsd:frame-check", error.GetBaseException().ToString()); Dispose(); }
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        Flow?.Dispose(); Flow = null;
        if (After) { After!.Release(); Object.Destroy(After); }
        if (Readback) Object.Destroy(Readback);
        After = null; Readback = null;
    }
}
