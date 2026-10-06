using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Explicit --lsd-render-check diagnostic, run once in the menu. Test the actual
// GPU shader/bindings using owned offscreen images, without loading a save.
internal static class ShaderCheck
{
    internal static void Run()
    {
        Il2CppAssetBundle? bundle = null;
        Material? material = null;
        Texture2D? input = null, readback = null;
        RenderTexture? target = null;
        var previous = RenderTexture.active;
        var flip = Shader.GetGlobalFloat("_FlipY");
        var blit = Shader.GetGlobalTexture("_BlitTexture");
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Personal.LSDPreview.Peripheral.bundle")!;
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            bundle = Il2CppAssetBundleManager.LoadFromMemory(new Il2CppStructArray<byte>(bytes.ToArray()));
            var shader = bundle.LoadAsset<Shader>("assets/personal/lsdperipheral.shader");
            material = new Material(shader);
            const int width = 640, height = 360;
            input = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            input.filterMode = FilterMode.Bilinear;
            input.wrapMode = TextureWrapMode.Clamp;
            var colors = new Il2CppStructArray<Color32>(width * height);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    colors[y * width + x] = new Color32((byte)(255 * x / width), (byte)(255 * y / height),
                        (byte)(((x / 8 + y / 8) & 1) * 255), 255);
            input.SetPixels32(colors);
            input.Apply(false, false);
            target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create();
            readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            material.SetFloat("_NoiseScale", width / (float)height);
            var inner = RippleRange.FocusRadius(5, 65);
            material.SetVector("_PanSpeed", new Vector4(inner, RippleRange.FocusOuter(inner), 0, 0));
            material.SetFloat("_DoesBounce", 1);
            Color32[] Render(float strength, float time)
            {
                material.SetFloat("_Blend", strength / height);
                material.SetFloat("_Amplitude", time);
                var commands = new CommandBuffer();
                try
                {
                    commands.SetRenderTarget(target);
                    commands.SetViewport(new Rect(0, 0, width, height));
                    commands.SetGlobalFloat("_FlipY", 1);
                    commands.SetGlobalTexture("_BlitTexture", input);
                    commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                finally { commands.Release(); }
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                readback.Apply(false, false);
                return readback.GetPixels32().ToArray();
            }
            var baseline = Render(0, 0);
            var first = Render(5.4f, 0);
            var second = Render(5.4f, 2.4f);
            var changed = 0;
            var moving = 0;
            var centerChanged = 0;
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    bool Different(Color32 a, Color32 b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 3;
                    if (Different(baseline[index], second[index])) changed++;
                    if (Different(first[index], second[index])) moving++;
                    if (Math.Abs(x - width / 2) < 12 && Math.Abs(y - height / 2) < 12
                        && Different(baseline[index], second[index])) centerChanged++;
                }
            if (changed < 1000 || moving < 1000 || centerChanged != 0)
                throw new InvalidOperationException($"GPU warp check failed: changed {changed}, moving {moving}, center changed {centerChanged}.");
            MelonLoader.MelonLogger.Msg($"LSD GPU warp check passed: {changed} changed pixels, {moving} time-varying pixels, {centerChanged} protected-center changes, at the default 5.4px bound.");
        }
        catch (Exception error)
        {
            ModSupport.WarnOnce("lsd:shader-check", error.GetBaseException().ToString());
        }
        finally
        {
            RenderTexture.active = previous;
            Shader.SetGlobalFloat("_FlipY", flip);
            Shader.SetGlobalTexture("_BlitTexture", blit);
            if (target) { target!.Release(); Object.Destroy(target); }
            if (readback) Object.Destroy(readback);
            if (input) Object.Destroy(input);
            if (material) Object.Destroy(material);
            bundle?.Unload(true);
        }
    }
}
