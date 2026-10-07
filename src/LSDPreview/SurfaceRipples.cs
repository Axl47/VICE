using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Warp a completed-frame capture into an owned texture, then let Unity's
// ordinary blit present it. The shader never draws directly to the backbuffer.
internal sealed class SurfaceRipples : IDisposable
{
    private readonly Camera Camera;
    private Il2CppAssetBundle? Bundle;
    private Material? Material;
    private RenderTexture? Capture, Warped;
    private float Pixels, Elapsed, Closure, PatternAmount, PatternMotion;
    private int Width, Height;
    private bool Disposed, Faulted, Reported;
    private readonly bool Diagnostic;
    internal RenderTexture? LastCapture => Capture;

    internal SurfaceRipples(Camera camera, bool diagnostic = false)
    {
        Camera = camera;
        Diagnostic = diagnostic;
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Personal.LSDPreview.Peripheral.bundle")
                ?? throw new InvalidOperationException("The authored flow shader is missing.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            Bundle = Il2CppAssetBundleManager.LoadFromMemory(new Il2CppStructArray<byte>(memory.ToArray()));
            var shader = Bundle.LoadAsset<Shader>("assets/personal/lsdperipheral.shader");
            if (!shader || !shader.isSupported) throw new InvalidOperationException("The authored flow shader is unsupported by this graphics device.");
            Material = new Material(shader) { name = "Personal LSD completed-frame flow", hideFlags = HideFlags.DontSave };
            Material.SetFloat("_DoesBounce", 0); // No gaze mask on the default path.
            MelonLoader.MelonLogger.Msg("LSD completed-frame flow ready: end-of-frame draw, authored shader, full image, no gaze mask.");
        }
        catch { Dispose(); throw; }
    }

    internal void Tick(float elapsed, float amount, float strength, float motion, float movement,
        float closedEyes = 0, float patternAmount = 0)
    {
        if (Disposed || Faulted) return;
        Elapsed = elapsed;
        Pixels = RippleRange.Pixels(elapsed, amount, strength, motion, movement);
        Closure = TripTimeline.Safe(closedEyes, 0, 0, 1);
        PatternAmount = TripTimeline.Safe(patternAmount, 0, 0, 1);
        PatternMotion = TripTimeline.Safe(motion, 0, 0, 1);
    }

    internal void RenderFrame()
    {
        if (Disposed || Faulted || (Pixels <= 0.001f && Closure <= 0 && !Diagnostic) || !Camera || Material is null || !Material) return;
        if (Screen.width < 1 || Screen.height < 1) return;
        try
        {
            if (!Capture || Width != Screen.width || Height != Screen.height)
            {
                ReleaseCapture();
                Width = Screen.width;
                Height = Screen.height;
                if ((long)Width * Height > 16777216) throw new InvalidOperationException("Screen dimensions exceed the flow texture budget.");
                Capture = MakeTexture("Personal LSD completed frame");
                Warped = MakeTexture("Personal LSD warped frame");
            }
            ScreenCapture.CaptureScreenshotIntoRenderTexture(Capture);
            Material.SetFloat("_NoiseScale", Width / (float)Height);
            Material.SetFloat("_Blend", Pixels / Height);
            Material.SetFloat("_Amplitude", Elapsed);
            // Negative mode reuses the existing constant bindings, preserving
            // the open-eye program and its optional positive-mode gaze mask.
            Material.SetFloat("_DoesBounce", Closure > 0 ? -1 - PatternMotion : 0);
            Material.SetVector("_PanSpeed", new Vector4(Closure, PatternAmount, 0, 0));
            Draw(Material, Capture!, Warped!, Width, Height);
            Present(Warped!);
            if (!Reported && Elapsed >= 10)
            {
                Reported = true;
                MelonLoader.MelonLogger.Msg($"LSD completed-frame flow rendered: {Width}x{Height}, bound {Pixels:F2}px, elapsed {Elapsed:F2}s, gaze mask off; uneven flow/ripples, native screen presentation.");
            }
        }
        catch (Exception error)
        {
            Faulted = true;
            ModSupport.WarnOnce("lsd:flow:frame", $"LSD completed-frame flow stopped: {error.GetBaseException().Message}");
        }
    }

    private RenderTexture MakeTexture(string name)
    {
        var target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
        { name = name, hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        if (!target.Create()) { Object.Destroy(target); throw new InvalidOperationException("The completed-frame texture could not be created."); }
        return target;
    }

    // Both runtime and offscreen diagnostics use this texture-to-texture draw.
    internal static void Draw(Material material, Texture source, RenderTexture destination, int width, int height)
    {
        var previousTexture = Shader.GetGlobalTexture("_BlitTexture");
        var previousTarget = RenderTexture.active;
        var previousSrgb = GL.sRGBWrite;
        try
        {
            Graphics.SetRenderTarget(destination);
            GL.Viewport(new Rect(0, 0, width, height));
            GL.sRGBWrite = destination.sRGB;
            Shader.SetGlobalTexture("_BlitTexture", source);
            if (!material.SetPass(0)) throw new InvalidOperationException("The completed-frame shader pass could not be bound.");
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 3, 1);
        }
        finally
        {
            Shader.SetGlobalTexture("_BlitTexture", previousTexture);
            GL.sRGBWrite = previousSrgb;
            RenderTexture.active = previousTarget;
        }
    }

    internal static void Present(RenderTexture source)
    {
        var previous = RenderTexture.active;
        var previousSrgb = GL.sRGBWrite;
        try
        {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            // A screen capture has top-origin rows on Direct3D. Correct this
            // at presentation once, independently of camera-global state.
            if (SystemInfo.graphicsUVStartsAtTop)
                Graphics.Blit(source, (RenderTexture)null!, new Vector2(1, -1), new Vector2(0, 1));
            else Graphics.Blit(source, (RenderTexture)null!);
        }
        finally { GL.sRGBWrite = previousSrgb; RenderTexture.active = previous; }
    }

    private void ReleaseCapture()
    {
        if (Capture) { Capture!.Release(); Object.Destroy(Capture); }
        if (Warped) { Warped!.Release(); Object.Destroy(Warped); }
        Capture = Warped = null;
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        ReleaseCapture();
        if (Material) Object.Destroy(Material);
        Material = null;
        Bundle?.Unload(true);
        Bundle = null;
    }
}
