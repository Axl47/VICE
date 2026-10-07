using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using S1LSD.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.S1LSDComparison;

// Original MIT S1LSD shader/uniforms; owned completed-frame presentation replaces
// its camera event/native pass. The disposable comparison also affects phone/UI.
internal sealed class ComparisonRenderer : IDisposable
{
    private Il2CppAssetBundle? Bundle;
    private Material? Material;
    private RenderTexture? Capture, Output, History;
    private int Width, Height, InvalidFrames = 2;
    private Quaternion PreviousRotation;
    private Vector3 PreviousPosition;
    private float PreviousTime = -1;
    internal long Frames { get; private set; }
    internal RenderTexture? LastCapture => Capture;
    internal Material ShaderMaterial => Material!;

    internal ComparisonRenderer()
    {
        try {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Comparison.Shaders.bundle")
                ?? throw new InvalidOperationException("S1LSD comparison shader bundle missing.");
            using var bytes = new MemoryStream(); stream.CopyTo(bytes);
            Bundle = Il2CppAssetBundleManager.LoadFromMemory(new Il2CppStructArray<byte>(bytes.ToArray()));
            if (Bundle is null) throw new InvalidOperationException("The adapted S1LSD comparison bundle could not be loaded.");
            var shader = Bundle.LoadAsset<Shader>("assets/s1lsd/perception.shader");
            if (!shader || !shader.isSupported) throw new InvalidOperationException("The original S1LSD perception shader is unsupported.");
            Material = new Material(shader) { name = "S1LSD disposable comparison", hideFlags = HideFlags.DontSave };
            Material.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
        } catch { Dispose(); throw; }
    }

    internal void Render(Camera camera, in ShaderTargets targets, float seconds)
    {
        if (!camera || !Material || Screen.width < 1 || Screen.height < 1) return;
        if (!Capture || Width != Screen.width || Height != Screen.height) {
            ReleaseTextures(); Width = Screen.width; Height = Screen.height;
            if ((long)Width * Height > 16777216) throw new InvalidOperationException("Comparison screen exceeds the texture budget.");
            Capture = MakeTexture("Comparison source"); Output = MakeTexture("Comparison output");
            History = MakeTexture("Comparison history"); InvalidFrames = 2; PreviousTime = -1;
        }
        var previous = RenderTexture.active;
        try {
            ScreenCapture.CaptureScreenshotIntoRenderTexture(Capture!);
            SetUniforms(camera, targets, seconds);
            Draw(Material!, Capture!, Output!, Width, Height);
            Present(Output!);
            // History stores the same processed image that was presented, as in
            // the original perception + history pass ordering. Never sample/write
            // the same render texture in a single draw.
            Graphics.Blit(Output!, History!);
            Frames++;
        } finally { RenderTexture.active = previous; }
    }

    private RenderTexture MakeTexture(string name)
    {
        var target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
        { name = name, hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        if (!target.Create()) { Object.Destroy(target); throw new InvalidOperationException("Comparison render texture allocation failed."); }
        return target;
    }

    internal void SetUniforms(Camera camera, in ShaderTargets t, float seconds)
    {
        var m = Material!;
        BindTargets(m, t, seconds, Width, Height);
        var rotation = camera.transform.rotation;
        var position = camera.transform.position;
        var now = Time.unscaledTime;
        var delta = PreviousTime < 0 ? 0 : Mathf.Clamp(now - PreviousTime, 0, 0.25f);
        if (Quaternion.Angle(rotation, PreviousRotation) > 35 || (position - PreviousPosition).sqrMagnitude > 9)
            InvalidFrames = Math.Max(InvalidFrames, 2);
        var reproject = Matrix4x4.Rotate(Quaternion.Inverse(PreviousRotation) * rotation);
        if (SystemInfo.graphicsUVStartsAtTop) {
            var flip = Matrix4x4.Scale(new Vector3(1, -1, 1));
            reproject = flip * reproject * flip;
        }
        m.SetMatrix("_S1Reproject", reproject);
        var lens = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        m.SetVector("_S1Lens", new Vector4(lens * camera.aspect, lens, 0, 0));
        m.SetTexture("_S1History", History!);
        m.SetFloat("_S1HistoryValid", t.TrailTau > 0.001f && InvalidFrames <= 0 ? 1 : 0);
        m.SetFloat("_S1TrailKeep", delta > 0 && t.TrailTau > 0.001f ? Mathf.Exp(-delta / Mathf.Max(0.01f, t.TrailTau)) : 0);
        if (InvalidFrames > 0) InvalidFrames--;
        PreviousRotation = rotation; PreviousPosition = position; PreviousTime = now;
    }

    // Values and scaling preserved from the original LsdShaders.SetUniforms.
    internal static void BindTargets(Material m, in ShaderTargets t, float seconds, int width, int height)
    {
        m.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
        m.SetVector("_S1Screen", new Vector4(width, height, 1f / width, 1f / height));
        m.SetFloat("_S1Time", seconds); m.SetFloat("_S1Crawl", t.Crawl);
        m.SetFloat("_S1CrawlSpeed", t.CrawlSpeed); m.SetFloat("_S1Breathe", t.Breathe);
        m.SetFloat("_S1Gaze", t.Gaze); m.SetFloat("_S1Vibrance", t.Vibrance);
        m.SetVector("_S1HueBands", new Vector4(t.HueShadows, t.HueMids, t.HueHighs, 0));
        m.SetFloat("_S1LocalContrast", t.LocalContrast); m.SetFloat("_S1Fringe", t.Fringe * height / 1080f);
        m.SetFloat("_S1Warmth", t.Warmth); m.SetFloat("_S1Patterns", t.Patterns);
        m.SetVector("_S1PatternMix", new Vector4(t.PatternLattice, t.PatternTunnel, t.PatternSpiral, t.PatternCobweb));
        m.SetFloat("_S1PatternScale", t.PatternScale); m.SetFloat("_S1Tunnel", t.Tunnel);
        m.SetFloat("_S1Shadows", t.Shadows); m.SetFloat("_S1Heart", t.Heart);
        m.SetFloat("_S1TrailBright", t.TrailBright);
    }

    internal static void Draw(Material material, Texture source, RenderTexture destination, int width, int height)
    {
        var previousTexture = Shader.GetGlobalTexture("_BlitTexture");
        var previousTarget = RenderTexture.active;
        var previousSrgb = GL.sRGBWrite;
        try {
            Graphics.SetRenderTarget(destination);
            GL.Viewport(new Rect(0, 0, width, height)); GL.sRGBWrite = destination.sRGB;
            Shader.SetGlobalTexture("_BlitTexture", source);
            material.SetTexture("_BlitTexture", source);
            if (!material.SetPass(0)) throw new InvalidOperationException("The S1LSD comparison shader pass could not be bound.");
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 3, 1);
        } finally {
            Shader.SetGlobalTexture("_BlitTexture", previousTexture);
            GL.sRGBWrite = previousSrgb; RenderTexture.active = previousTarget;
        }
    }

    private static void Present(RenderTexture source)
    {
        var previous = RenderTexture.active; var srgb = GL.sRGBWrite;
        try {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            if (SystemInfo.graphicsUVStartsAtTop)
                Graphics.Blit(source, (RenderTexture)null!, new Vector2(1, -1), new Vector2(0, 1));
            else Graphics.Blit(source, (RenderTexture)null!);
        } finally { GL.sRGBWrite = srgb; RenderTexture.active = previous; }
    }

    private void ReleaseTextures()
    {
        foreach (var texture in new[] { Capture, Output, History })
            if (texture) {
                if (RenderTexture.active == texture) RenderTexture.active = null!;
                texture!.Release(); Object.Destroy(texture);
            }
        Capture = Output = History = null;
    }

    public void Dispose()
    {
        ReleaseTextures();
        if (Material) Object.Destroy(Material);
        Material = null; Bundle?.Unload(true); Bundle = null;
    }
}
