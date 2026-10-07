using S1LSD.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.S1LSDComparison;

// Opt-in menu-only check: no player, items, saves or gameplay are needed.
internal sealed class ComparisonCheck : IDisposable
{
    private readonly ComparisonRenderer Renderer = new();
    private readonly ComparisonSession Session;
    private readonly Camera Camera;
    private RenderTexture? After;
    private Texture2D? Readback;
    private int Phase, Frames;
    internal bool Complete { get; private set; }
    private readonly float Started = Time.realtimeSinceStartup;

    internal ComparisonCheck(ComparisonSession session, Camera camera)
    {
        Session = session; Camera = camera;
        try {
            Offscreen();
            using (var volume = new ComparisonVolume(camera)) {
                var input = Session.Inputs(); var grade = Session.Grade(input);
                volume.Tick(grade);
            }
            MelonLoader.MelonLogger.Msg("S1LSD comparison VOLUME PASS: owned grade components created, updated and released.");
            Session.Start(360);
        }
        catch { Dispose(); throw; }
    }

    private static bool Different(Color32 a, Color32 b) =>
        Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 3;

    private void Offscreen()
    {
        const int width = 1280, height = 720;
        Texture2D? input = null, read = null;
        RenderTexture? source = null, target = null;
        var previous = RenderTexture.active;
        try {
            input = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            var pixels = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(width * height);
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                pixels[y * width + x] = new Color32((byte)(255 * x / width), (byte)(255 * y / height), (byte)(((x / 16 + y / 16) & 1) * 255), 255);
            input.SetPixels32(pixels); input.Apply(false, false);
            source = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            if (!source.Create() || !target.Create()) throw new InvalidOperationException("Diagnostic textures could not be allocated.");
            Graphics.Blit(input, source); read = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            var material = Renderer.ShaderMaterial;
            material.SetTexture("_S1History", source); material.SetFloat("_S1HistoryValid", 0);
            material.SetFloat("_S1TrailKeep", 0); material.SetMatrix("_S1Reproject", Matrix4x4.identity);
            material.SetVector("_S1Lens", new Vector4(1, 0.6f, 0, 0));
            Color32[] Render(ShaderTargets t, float seconds) {
                ComparisonRenderer.BindTargets(material, t, seconds, width, height);
                ComparisonRenderer.Draw(material, source, target, width, height);
                RenderTexture.active = target; read.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                read.Apply(false, false); return read.GetPixels32().ToArray();
            }
            var zero = new ShaderTargets { PatternScale = 1, TrailBright = 1 };
            var baseline = Render(zero, 0); var expected = input.GetPixels32().ToArray();
            var errors = baseline.Where((c, i) => Different(c, expected[i])).Count();
            if (errors > width * height / 100) throw new InvalidOperationException($"Original shader identity failed: {errors} pixels changed at zero strength.");
            Session.Start(360); Session.Tick(0.1f);
            var inputs = Session.Inputs(); var strong = Session.Shader(inputs);
            var first = Render(strong, 20); strong.Breathe *= -1;
            var second = Render(strong, 30);
            var motion = first.Where((c, i) => Different(c, second[i])).Count();
            if (motion < 1000) throw new InvalidOperationException($"Original shader did not animate: {motion} changing pixels.");
            MelonLoader.MelonLogger.Msg($"S1LSD comparison OFFSCREEN PASS: {errors} identity errors; {motion} time-varying pixels at tier 360.");
        } finally {
            RenderTexture.active = previous;
            foreach (var texture in new[] { source, target }) if (texture) { texture!.Release(); Object.Destroy(texture); }
            if (input) Object.Destroy(input); if (read) Object.Destroy(read);
        }
    }

    internal void RenderFrame()
    {
        if (Complete) return;
        if (Time.realtimeSinceStartup - Started > 10) throw new InvalidOperationException("The visible comparison frame check timed out.");
        Session.Tick(Mathf.Clamp(Time.unscaledDeltaTime, 0, 0.1f));
        var input = Session.Inputs();
        var targets = Phase == 0 ? new ShaderTargets { PatternScale = 1, TrailBright = 1 } : Session.Shader(input);
        Renderer.Render(Camera, targets, Session.VisualTime);
        if (Renderer.LastCapture is not { } before || ++Frames < 3) return;
        if (!After) {
            After = new RenderTexture(before.width, before.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            if (!After.Create()) throw new InvalidOperationException("Final-frame diagnostic allocation failed.");
            Readback = new Texture2D(before.width, before.height, TextureFormat.RGBA32, false);
        }
        ScreenCapture.CaptureScreenshotIntoRenderTexture(After!);
        var previous = RenderTexture.active;
        Color32[] Read(RenderTexture t) {
            RenderTexture.active = t; Readback!.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0, false);
            Readback.Apply(false, false); return Readback.GetPixels32().ToArray();
        }
        Color32[] a, b;
        try { a = Read(before); b = Read(After!); }
        finally { RenderTexture.active = previous; }
        var visible = a.Count(c => c.r > 8 || c.g > 8 || c.b > 8);
        if (visible < 1000) throw new InvalidOperationException("The comparison screen is not rendering a visible image.");
        var changed = a.Where((c, i) => Different(c, b[i])).Count();
        if (Phase == 0) {
            if (changed > Math.Max(64, visible / 1000)) throw new InvalidOperationException($"Comparison final-frame identity failed: {changed} pixels.");
            MelonLoader.MelonLogger.Msg($"S1LSD comparison FINAL-FRAME IDENTITY PASS: {changed} changed pixels.");
            Phase = 1; Frames = 0;
        } else {
            if (changed < 100) throw new InvalidOperationException("Original perception shader did not affect the final screen.");
            MelonLoader.MelonLogger.Msg($"S1LSD comparison FINAL-FRAME MOTION PASS: {changed} changed pixels. No save loaded.");
            Complete = true; Dispose();
        }
    }

    public void Dispose()
    {
        Complete = true; Session.Stop(); Renderer.Dispose();
        if (After) { After!.Release(); Object.Destroy(After); }
        if (Readback) Object.Destroy(Readback); After = null; Readback = null;
    }
}
