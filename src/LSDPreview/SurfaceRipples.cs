using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Warp the completed frame with the verified authored shader. A clear-center
// mask and an early camera-color target no longer suppress the visible result.
internal sealed class SurfaceRipples : IDisposable
{
    private readonly Camera Camera;
    private Il2CppAssetBundle? Bundle;
    private Material? Material;
    private RenderTexture? Capture;
    private float Pixels, Elapsed;
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

    internal void Tick(float elapsed, float amount, float strength, float motion, float movement)
    {
        if (Disposed || Faulted) return;
        Elapsed = elapsed;
        Pixels = RippleRange.Pixels(elapsed, amount, strength, motion, movement);
    }

    internal void RenderFrame()
    {
        if (Disposed || Faulted || (Pixels <= 0.001f && !Diagnostic) || !Camera || Material is null || !Material) return;
        if (Screen.width < 1 || Screen.height < 1) return;
        try
        {
            if (!Capture || Width != Screen.width || Height != Screen.height)
            {
                ReleaseCapture();
                Width = Screen.width;
                Height = Screen.height;
                Capture = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
                { name = "Personal LSD completed frame", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                if (!Capture.Create()) throw new InvalidOperationException("The completed-frame texture could not be created.");
            }
            ScreenCapture.CaptureScreenshotIntoRenderTexture(Capture);
            Material.SetFloat("_NoiseScale", Width / (float)Height);
            Material.SetFloat("_Blend", Pixels / Height);
            Material.SetFloat("_Amplitude", Elapsed);
            Material.SetFloat("_DoesBounce", 0);
            Draw(Material, Capture!, null, Width, Height, SystemInfo.graphicsUVStartsAtTop ? 1 : 0);
            if (!Reported && Elapsed >= 10)
            {
                Reported = true;
                MelonLoader.MelonLogger.Msg($"LSD completed-frame flow rendered: {Width}x{Height}, bound {Pixels:F2}px, elapsed {Elapsed:F2}s, gaze mask off; radial breathing and flowing detail share the final image.");
            }
        }
        catch (Exception error)
        {
            Faulted = true;
            ModSupport.WarnOnce("lsd:flow:frame", $"LSD completed-frame flow stopped: {error.GetBaseException().Message}");
        }
    }

    // The offscreen diagnostic calls this same binding/draw path. A null
    // destination means the actual screen, after all cameras and UI rendered.
    internal static void Draw(Material material, Texture source, RenderTexture? destination, int width, int height, float flip)
    {
        var previousFlip = Shader.GetGlobalFloat("_FlipY");
        var previousTexture = Shader.GetGlobalTexture("_BlitTexture");
        var previousTarget = RenderTexture.active;
        var previousSrgb = GL.sRGBWrite;
        try
        {
            Graphics.SetRenderTarget(destination!);
            GL.Viewport(new Rect(0, 0, width, height));
            GL.sRGBWrite = destination ? destination!.sRGB : QualitySettings.activeColorSpace == ColorSpace.Linear;
            Shader.SetGlobalFloat("_FlipY", flip);
            Shader.SetGlobalTexture("_BlitTexture", source);
            if (!material.SetPass(0)) throw new InvalidOperationException("The completed-frame shader pass could not be bound.");
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 3, 1);
        }
        finally
        {
            Shader.SetGlobalFloat("_FlipY", previousFlip);
            Shader.SetGlobalTexture("_BlitTexture", previousTexture);
            GL.sRGBWrite = previousSrgb;
            RenderTexture.active = previousTarget;
        }
    }

    private void ReleaseCapture()
    {
        if (Capture) { Capture!.Release(); Object.Destroy(Capture); }
        Capture = null;
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
