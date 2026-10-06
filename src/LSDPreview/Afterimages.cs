using Il2CppInterop.Runtime;
using Il2CppScheduleOne.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PersonalMods.LSDPreview;

// GPU-only history. No screenshot files, pixel readback or second world camera.
internal sealed class Afterimages : IDisposable
{
    private readonly Camera Camera;
    private GameObject? Root;
    private readonly RawImage[] Images = new RawImage[2];
    private readonly RenderTexture?[] History = new RenderTexture?[3];
    private RenderTexture? Capture;
    private Il2CppSystem.Action<ScriptableRenderContext, Camera>? Callback;
    private int Width, Height, WriteIndex, Count;
    private float Amount, NextCapture;
    private Vector3 PreviousPosition;
    private Quaternion PreviousRotation;
    private bool Disposed, Faulted;

    internal Afterimages(Camera camera)
    {
        Camera = camera;
        try
        {
            Root = new GameObject("Personal LSD afterimages") { hideFlags = HideFlags.DontSave };
            var canvas = Root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var hud = HUD.Instance;
            canvas.sortingOrder = hud && hud.canvas ? hud.canvas.sortingOrder - 1 : -10;
            var group = Root.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            for (var i = 0; i < Images.Length; i++)
            {
                var go = new GameObject("Delayed world image " + i) { hideFlags = HideFlags.DontSave };
                var rect = go.AddComponent<RectTransform>();
                rect.SetParent(Root.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                Images[i] = go.AddComponent<RawImage>();
                Images[i].raycastTarget = false;
                // Screen capture retains the graphics API's vertical origin.
                // RawImage uses bottom-up UI UVs; correct top-origin captures once.
                Images[i].uvRect = SystemInfo.graphicsUVStartsAtTop
                    ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
                Images[i].enabled = false;
            }
            Root.SetActive(false);
            PreviousPosition = camera.transform.position;
            PreviousRotation = camera.transform.rotation;
            Callback = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<ScriptableRenderContext, Camera>>(
                (Action<ScriptableRenderContext, Camera>)OnCameraRendered);
            RenderPipelineManager.add_endCameraRendering(Callback);
        }
        catch { Dispose(); throw; }
    }

    internal void Tick(float amount)
    {
        if (Disposed || Faulted || Root is null || !Root || !Camera) return;
        Amount = amount;
        if (amount <= 0.001f)
        {
            Root.SetActive(false);
            Count = 0;
            NextCapture = 0;
            return;
        }
        var position = Camera.transform.position;
        var rotation = Camera.transform.rotation;
        var travel = Vector3.Distance(position, PreviousPosition);
        var turn = Quaternion.Angle(rotation, PreviousRotation);
        if (travel > 18 || turn > 100) Count = 0;
        PreviousPosition = position;
        PreviousRotation = rotation;
        Root.SetActive(true);
        // Keep composited feedback low. More movement increases the visible echo.
        var motion = 0.3f + 0.7f * Mathf.Clamp01(travel * 8 + turn / 4);
        Images[0].color = new Color(0.94f, 1, 1, amount * motion * 0.14f);
        Images[1].color = new Color(1, 0.94f, 1, amount * motion * 0.065f);
        RefreshImages();
    }

    private void OnCameraRendered(ScriptableRenderContext context, Camera renderedCamera)
    {
        if (Disposed || Faulted || Amount <= 0.001f || !renderedCamera || !Camera) return;
        try
        {
            var finalCamera = Camera;
            var data = Camera.GetComponent<UniversalAdditionalCameraData>();
            if (data && data.renderType == CameraRenderType.Base)
            foreach (var overlay in data.cameraStack)
                if (overlay && overlay.enabled && overlay.gameObject.activeInHierarchy) finalCamera = overlay;
            if (renderedCamera.Pointer != finalCamera.Pointer || Time.realtimeSinceStartup < NextCapture) return;
            EnsureTextures();
            if (Capture is null || !Capture) return;
            var previousTarget = RenderTexture.active;
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(Capture);
                Graphics.Blit(Capture, History[WriteIndex]);
            }
            finally { RenderTexture.active = previousTarget; }
            WriteIndex = (WriteIndex + 1) % History.Length;
            Count = Math.Min(History.Length, Count + 1);
            NextCapture = Time.realtimeSinceStartup + 0.12f;
            RefreshImages();
        }
        catch (Exception error)
        {
            Faulted = true;
            if (Root is not null && Root) Root.SetActive(false);
            ModSupport.WarnOnce("lsd:afterimage-capture", $"LSD afterimages were disabled: {error.Message}");
        }
    }

    private void RefreshImages()
    {
        Images[0].enabled = Count >= 2;
        Images[1].enabled = Count >= 3;
        if (Count >= 2) Images[0].texture = History[(WriteIndex + 1) % History.Length];
        if (Count >= 3) Images[1].texture = History[WriteIndex];
    }

    private void EnsureTextures()
    {
        if (Width == Screen.width && Height == Screen.height && Capture is not null && Capture) return;
        ReleaseTextures();
        Width = Screen.width;
        Height = Screen.height;
        if (Width <= 0 || Height <= 0 || (long)Width * Height > 16777216)
            throw new InvalidOperationException("Screen dimensions exceed the afterimage texture budget.");
        Capture = MakeTexture(Width, Height, "LSD current frame");
        var historyWidth = Math.Min(1280, Math.Max(32, Width / 2));
        var historyHeight = Math.Max(32, (int)((long)Height * historyWidth / Width));
        for (var i = 0; i < History.Length; i++) History[i] = MakeTexture(historyWidth, historyHeight, "LSD history " + i);
        Count = WriteIndex = 0;
    }

    private static RenderTexture MakeTexture(int width, int height, string name)
    {
        var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        { name = name, hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        if (!texture.Create()) { Object.Destroy(texture); throw new InvalidOperationException("An afterimage render texture could not be created."); }
        return texture;
    }

    private void ReleaseTextures()
    {
        foreach (var image in Images) if (image) { image.texture = null; image.enabled = false; }
        if (Capture is not null && Capture) { Capture.Release(); Object.Destroy(Capture); }
        Capture = null;
        for (var i = 0; i < History.Length; i++)
        {
            var texture = History[i];
            if (texture is not null && texture) { texture.Release(); Object.Destroy(texture); }
            History[i] = null;
        }
        Count = WriteIndex = 0;
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        Amount = 0;
        if (Callback is not null) RenderPipelineManager.remove_endCameraRendering(Callback);
        Callback = null;
        if (Root is not null && Root) { Root.SetActive(false); Object.Destroy(Root); }
        ReleaseTextures();
        Root = null;
    }
}
