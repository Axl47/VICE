using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// Offscreen test of the exact normal binding/draw path. FrameCheck separately
// checks the completed image when a visible game window is rendering.
internal static class ShaderCheck
{
    internal static void Run()
    {
        Il2CppAssetBundle? bundle = null;
        Material? material = null;
        Texture2D? input = null, readback = null;
        RenderTexture? source = null, target = null;
        var previous = RenderTexture.active;
        var blit = Shader.GetGlobalTexture("_BlitTexture");
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Personal.LSDPreview.Peripheral.bundle")!;
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            bundle = Il2CppAssetBundleManager.LoadFromMemory(new Il2CppStructArray<byte>(bytes.ToArray()));
            var shader = bundle.LoadAsset<Shader>("assets/personal/lsdperipheral.shader");
            material = new Material(shader);
            const int width = 1280, height = 720;
            input = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            var colors = new Il2CppStructArray<Color32>(width * height);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    colors[y * width + x] = new Color32((byte)(255 * x / width), (byte)(255 * y / height),
                        (byte)(((x / 16 + y / 16) & 1) * 255), 255);
            input.SetPixels32(colors); input.Apply(false, false);
            source = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            source.Create(); target.Create(); Graphics.Blit(input, source);
            readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            material.SetFloat("_NoiseScale", width / (float)height);
            material.SetFloat("_DoesBounce", 0);
            Color32[] Render(float pixels, float seconds)
            {
                material.SetFloat("_Blend", pixels / height);
                material.SetFloat("_Amplitude", seconds);
                SurfaceRipples.Draw(material, source, target, width, height);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false); readback.Apply(false, false);
                return readback.GetPixels32().ToArray();
            }
            var baseline = Render(0, 0);
            var expected = input.GetPixels32().ToArray();
            var identityErrors = baseline.Where((c, i) => Math.Abs(c.r - expected[i].r)
                + Math.Abs(c.g - expected[i].g) + Math.Abs(c.b - expected[i].b) > 3).Count();
            if (identityErrors > baseline.Length / 100)
                throw new InvalidOperationException($"The normal shader draw changed {identityErrors} pixels at zero strength.");
            var first = Render(5.4f, 0);
            var second = Render(5.4f, 2.4f);
            var changed = 0; var moving = 0; var outerMoving = 0;
            for (var i = 0; i < baseline.Length; i++)
            {
                bool Different(Color32 a, Color32 b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 3;
                if (Different(baseline[i], second[i])) changed++;
                if (Different(first[i], second[i]))
                {
                    moving++;
                    var x = i % width; var y = i / width;
                    if (x < width / 4 || x >= width * 3 / 4 || y < height / 4 || y >= height * 3 / 4) outerMoving++;
                }
            }
            if (changed < 1000 || moving < 1000 || outerMoving < 1000) throw new InvalidOperationException($"Authored draw check failed: changed {changed}, moving {moving}, outer motion {outerMoving}.");
            MelonLoader.MelonLogger.Msg($"LSD authored draw flow check PASSED: {identityErrors} identity errors, {changed} changed pixels, {moving} time-varying pixels ({outerMoving} outside center) at 1280x720/default bound, gaze mask disabled.");
        }
        catch (Exception error) { ModSupport.WarnOnce("lsd:shader-check", error.GetBaseException().ToString()); }
        finally
        {
            RenderTexture.active = previous;
            Shader.SetGlobalTexture("_BlitTexture", blit);
            if (source) { source!.Release(); Object.Destroy(source); }
            if (target) { target!.Release(); Object.Destroy(target); }
            if (readback) Object.Destroy(readback);
            if (input) Object.Destroy(input);
            if (material) Object.Destroy(material);
            bundle?.Unload(true);
        }
    }
}
